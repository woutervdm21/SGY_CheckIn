using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SGY.CheckIn.Services;

public sealed record BackupFile(string Name, DateTime CreatedLocal, long SizeBytes);

/// <summary>
/// Full copies of the database, so a dead disk or a bad mistake doesn't lose the group's
/// records. Each backup is a complete, standalone snapshot (every youth, check-in, note and
/// setting at that moment), so restoring any one file brings everything back as of then.
/// </summary>
/// <remarks>
/// Uses SQLite's online backup, which takes a consistent copy while the app keeps running.
/// Backups go in a "backups" folder next to the database (on the Docker volume in
/// production) unless Backup:Directory points somewhere else, such as a folder that syncs
/// to OneDrive. The newest <see cref="Keep"/> are kept; older ones are deleted.
/// </remarks>
public sealed partial class BackupService
{
    private readonly string connectionString;
    private readonly ILogger<BackupService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    public BackupService(string connectionString, IConfiguration config, ILogger<BackupService> logger)
    {
        this.connectionString = connectionString;
        this.logger = logger;

        var databasePath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
        var configured = config["Backup:Directory"];
        Folder = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetDirectoryName(databasePath)!, "backups")
            : Path.GetFullPath(configured);
        Keep = Math.Max(1, config.GetValue("Backup:Keep", 12));
        Interval = TimeSpan.FromDays(Math.Max(1, config.GetValue("Backup:IntervalDays", 7)));
    }

    /// <summary>Where backups are written, as the server sees it.</summary>
    public string Folder { get; }

    /// <summary>How many backups to keep before the oldest is deleted.</summary>
    public int Keep { get; }

    /// <summary>How old the newest backup may get before the automatic one makes another.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Why the last backup attempt failed, or null if it worked.</summary>
    public string? LastError { get; private set; }

    public async Task<BackupFile> CreateBackupAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Folder);
            var name = $"sgy-checkin-{DateTime.Now:yyyy-MM-dd-HHmmss}.db";
            var path = Path.Combine(Folder, name);
            var temp = path + ".tmp";

            // Written under a temporary name and renamed when complete, so a half-written
            // file is never listed or downloaded as a backup.
            await Task.Run(() =>
            {
                using var source = new SqliteConnection(connectionString);
                using var destination = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString());
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }, ct);
            File.Move(temp, path, overwrite: true);

            DeleteOldBackups();
            LastError = null;
            logger.LogInformation("Backup saved to {Path}", path);
            return Describe(path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Backup to {Folder} failed", Folder);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Restores a backup someone has placed next to the database as "restore.db" (see the
    /// README). Called at startup before anything opens the database. The current data is
    /// backed up first, so a restore can itself be undone.
    /// </summary>
    public async Task ApplyPendingRestoreAsync()
    {
        var databasePath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
        var restorePath = Path.Combine(Path.GetDirectoryName(databasePath)!, "restore.db");
        if (!File.Exists(restorePath))
        {
            return;
        }

        if (!IsSqliteFile(restorePath))
        {
            logger.LogError("{Path} is not a SQLite database, so it was not restored. Delete it or replace it with a backup file.", restorePath);
            return;
        }

        if (File.Exists(databasePath))
        {
            var safetyCopy = await CreateBackupAsync();
            logger.LogWarning("Restoring a backup. The data it replaces was saved first as {Name}", safetyCopy.Name);
        }

        // Release the handles the safety copy left open, then swap the files. The old
        // write-ahead log belongs to the replaced database and must not be replayed into it.
        SqliteConnection.ClearAllPools();
        File.Copy(restorePath, databasePath, overwrite: true);
        File.Delete(databasePath + "-wal");
        File.Delete(databasePath + "-shm");
        File.Delete(restorePath);
        logger.LogWarning("Backup restored from {Path}", restorePath);
    }

    private static bool IsSqliteFile(string path)
    {
        Span<byte> header = stackalloc byte[16];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == 16 && header.SequenceEqual("SQLite format 3\0"u8);
    }

    /// <summary>Saved backups, newest first.</summary>
    public IReadOnlyList<BackupFile> List()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(Folder, "sgy-checkin-*.db")
            .Where(p => BackupName().IsMatch(Path.GetFileName(p)))
            .Select(Describe)
            .OrderByDescending(b => b.Name, StringComparer.Ordinal)];
    }

    /// <summary>Full path of a saved backup, or null if the name isn't one of ours.</summary>
    public string? PathOf(string name)
    {
        // Only exact backup names: this comes from a download URL, so nothing like "../".
        if (!BackupName().IsMatch(name))
        {
            return null;
        }

        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    private void DeleteOldBackups()
    {
        foreach (var old in List().Skip(Keep))
        {
            File.Delete(Path.Combine(Folder, old.Name));
        }

        // Leftovers from a backup interrupted mid-copy (e.g. the computer lost power).
        foreach (var temp in Directory.EnumerateFiles(Folder, "sgy-checkin-*.db.tmp"))
        {
            if (File.GetLastWriteTimeUtc(temp) < DateTime.UtcNow.AddHours(-1))
            {
                File.Delete(temp);
            }
        }
    }

    private static BackupFile Describe(string path)
    {
        var info = new FileInfo(path);
        return new BackupFile(info.Name, info.LastWriteTime, info.Length);
    }

    [GeneratedRegex(@"^sgy-checkin-\d{4}-\d{2}-\d{2}-\d{6}\.db$")]
    private static partial Regex BackupName();
}

/// <summary>
/// Makes the automatic backup. Church computers are often switched off at night, so rather
/// than waiting for a fixed time it checks shortly after the app starts and then every hour,
/// and makes a backup whenever the newest one is older than <see cref="BackupService.Interval"/>
/// (a week by default). A Friday night is enough to keep it current.
/// </summary>
public sealed class AutomaticBackupService(BackupService backups, ILogger<AutomaticBackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let startup (migrations, first requests) settle first.
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                var newest = backups.List().FirstOrDefault();
                if (newest is null || DateTime.Now - newest.CreatedLocal >= backups.Interval)
                {
                    try
                    {
                        await backups.CreateBackupAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Already logged and shown on the Settings page; try again next hour.
                        logger.LogDebug(ex, "Automatic backup will retry");
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The app is shutting down.
        }
    }
}
