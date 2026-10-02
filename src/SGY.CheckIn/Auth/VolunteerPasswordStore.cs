using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Auth;

/// <summary>
/// The shared volunteer password, kept in the database so an admin can reset it from the
/// Settings page. (The admin password stays in config: it is the way back in if the
/// volunteer one is forgotten.)
/// </summary>
/// <remarks>
/// A singleton holding the current hash in memory, so the sign-in check and the per-request
/// cookie check never hit the database. Each reset also issues a new stamp; volunteer
/// cookies carry the stamp they signed in with, and a cookie whose stamp no longer matches
/// is rejected — that's what signs out every volunteer device after a reset.
/// </remarks>
public sealed class VolunteerPasswordStore(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Cookie claim holding the stamp a volunteer signed in under.</summary>
    public const string StampClaim = "volunteer_password_stamp";

    private const string HashKey = "VolunteerPasswordHash";
    private const string StampKey = "VolunteerPasswordStamp";

    private sealed record Snapshot(string? Hash, string Stamp);

    private volatile Snapshot current = new(null, "");

    /// <summary>False until a volunteer password has been set; volunteer sign-in is off until then.</summary>
    public bool IsSet => current.Hash is not null;

    /// <summary>
    /// Changes on every reset. Empty until the first reset, which matches cookies issued
    /// before stamps existed, so upgrading doesn't sign anyone out.
    /// </summary>
    public string Stamp => current.Stamp;

    public bool Verify(string password) => PasswordHashing.Verify(current.Hash, password);

    /// <summary>
    /// Loads the stored password at startup. If none is stored yet, adopts
    /// <paramref name="configHash"/> (the old CheckInAuth:VolunteerPasswordHash setting) so an
    /// existing install keeps its volunteer password across the upgrade.
    /// </summary>
    public async Task LoadAsync(string? configHash, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var settings = await db.Settings
            .Where(s => s.Key == HashKey || s.Key == StampKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var hash = settings.GetValueOrDefault(HashKey);
        if (string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(configHash))
        {
            hash = configHash;
            db.Settings.Add(new AppSetting { Key = HashKey, Value = hash });
            await db.SaveChangesAsync(ct);
        }

        current = new Snapshot(string.IsNullOrEmpty(hash) ? null : hash, settings.GetValueOrDefault(StampKey) ?? "");
    }

    /// <summary>Sets a new volunteer password and signs out every volunteer device.</summary>
    public async Task SetAsync(string password, CancellationToken ct = default)
    {
        var next = new Snapshot(PasswordHashing.Hash(password), Guid.NewGuid().ToString("N"));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await UpsertAsync(db, HashKey, next.Hash, ct);
        await UpsertAsync(db, StampKey, next.Stamp, ct);
        await db.SaveChangesAsync(ct);

        current = next;
    }

    private static async Task UpsertAsync(AppDbContext db, string key, string? value, CancellationToken ct)
    {
        var setting = await db.Settings.FindAsync([key], ct);
        if (setting is null)
        {
            db.Settings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
    }
}
