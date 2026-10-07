using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Auth;

/// <summary>
/// The shared volunteer password for each group, kept in the database so an admin can reset
/// it from the Settings page. Each group has its own, so Kids volunteers can't open Youth
/// records and the other way round. (The admin password stays in config and works for
/// every group: it is the way back in if a volunteer one is forgotten.)
/// </summary>
/// <remarks>
/// A singleton holding the current hashes in memory, so the sign-in check and the per-request
/// cookie check never hit the database. Each reset also issues a new stamp for that group;
/// volunteer cookies carry the stamp they signed in with, and a cookie whose stamp no longer
/// matches is rejected — that's what signs out the group's volunteer devices after a reset.
/// </remarks>
public sealed class VolunteerPasswordStore(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Cookie claim holding the stamp a volunteer signed in under.</summary>
    public const string StampClaim = "volunteer_password_stamp";

    private sealed record Snapshot(string? Hash, string Stamp);

    private static readonly Snapshot NotSet = new(null, "");

    private readonly Lock gate = new();
    private volatile Dictionary<Group, Snapshot> current = [];

    /// <summary>False until the group's volunteer password has been set; its volunteers can't sign in until then.</summary>
    public bool IsSet(Group group) => Get(group).Hash is not null;

    /// <summary>
    /// Changes on every reset of this group's password. Empty until the first reset, which
    /// matches cookies issued before stamps existed, so upgrading doesn't sign anyone out.
    /// </summary>
    public string Stamp(Group group) => Get(group).Stamp;

    public bool Verify(Group group, string password) => PasswordHashing.Verify(Get(group).Hash, password);

    /// <summary>
    /// Loads the stored passwords at startup. If Youth has none stored yet, adopts
    /// <paramref name="configHash"/> (the old CheckInAuth:VolunteerPasswordHash setting, from
    /// when Youth was the only group) so an existing install keeps its volunteer password.
    /// </summary>
    public async Task LoadAsync(string? configHash, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var settings = await db.Settings
            .Where(s => s.Key.StartsWith("VolunteerPassword"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var loaded = new Dictionary<Group, Snapshot>();
        foreach (var group in Enum.GetValues<Group>())
        {
            var hash = settings.GetValueOrDefault(HashKey(group));
            if (group == Group.Youth && string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(configHash))
            {
                hash = configHash;
                db.Settings.Add(new AppSetting { Key = HashKey(group), Value = hash });
                await db.SaveChangesAsync(ct);
            }

            loaded[group] = new Snapshot(string.IsNullOrEmpty(hash) ? null : hash, settings.GetValueOrDefault(StampKey(group)) ?? "");
        }

        current = loaded;
    }

    /// <summary>Sets a group's volunteer password and signs out that group's volunteer devices.</summary>
    public async Task SetAsync(Group group, string password, CancellationToken ct = default)
    {
        var next = new Snapshot(PasswordHashing.Hash(password), Guid.NewGuid().ToString("N"));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await UpsertAsync(db, HashKey(group), next.Hash, ct);
        await UpsertAsync(db, StampKey(group), next.Stamp, ct);
        await db.SaveChangesAsync(ct);

        // Copy-on-write, so readers never see a dictionary mid-change; the lock stops two
        // admins resetting different groups at once from losing one of the changes.
        lock (gate)
        {
            current = new Dictionary<Group, Snapshot>(current) { [group] = next };
        }
    }

    private Snapshot Get(Group group) => current.GetValueOrDefault(group, NotSet);

    // Youth keeps the key names from before groups existed, so an existing install's
    // volunteer password and signed-in devices carry straight over.
    private static string HashKey(Group group) =>
        group == Group.Youth ? "VolunteerPasswordHash" : $"VolunteerPasswordHash:{group.ToKey()}";

    private static string StampKey(Group group) =>
        group == Group.Youth ? "VolunteerPasswordStamp" : $"VolunteerPasswordStamp:{group.ToKey()}";

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
