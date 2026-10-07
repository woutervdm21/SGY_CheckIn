using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>
/// Recording and querying check-ins. "Today" is computed in the server's local time zone
/// (not UTC) since that's what matters to staff at the door — set the TZ environment
/// variable on the container to the church's time zone so this lines up with the wall clock.
/// </summary>
public class CheckInService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Checks in someone from this group. Throws if they're not in it.</summary>
    public async Task<CheckInRecord> CheckInAsync(int youthId, Group group, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Youths.AnyAsync(y => y.Id == youthId && y.Group == group, ct))
        {
            throw new InvalidOperationException($"Person {youthId} isn't in {group}.");
        }
        var record = new CheckInRecord { YouthId = youthId, Timestamp = DateTime.UtcNow };
        db.CheckIns.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    /// <summary>
    /// Removes this youth's check-ins for today (normally just one): the "Undo check-in" on
    /// the check-in screen, for a youth checked in by mistake. Returns how many were removed.
    /// </summary>
    public async Task<int> UndoTodayCheckInAsync(int youthId, Group group, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns
            .Where(c => c.YouthId == youthId && c.Youth.Group == group && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Removes one check-in from the log. Unless <paramref name="allowPastDays"/> (admins),
    /// only today's can go, so a volunteer browsing an old date can't rewrite the history
    /// the dashboard reports on. Only the signed-in group's check-ins can go. Returns false
    /// if nothing was removed.
    /// </summary>
    public async Task<bool> UndoCheckInAsync(int checkInId, Group group, bool allowPastDays, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var removed = await db.CheckIns
            .Where(c => c.Id == checkInId && c.Youth.Group == group)
            .Where(c => allowPastDays || (c.Timestamp >= utcStart && c.Timestamp < utcEnd))
            .ExecuteDeleteAsync(ct);
        return removed > 0;
    }

    /// <summary>The most recent check-in for this youth today, if any — used to show
    /// an "already checked in" indicator without hard-blocking a re-check-in.</summary>
    public async Task<CheckInRecord?> GetTodayCheckInAsync(int youthId, Group group, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns.AsNoTracking()
            .Where(c => c.YouthId == youthId && c.Youth.Group == group && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>IDs of this group's people already checked in today, for annotating search results in bulk.</summary>
    public async Task<HashSet<int>> GetTodayCheckedInYouthIdsAsync(Group group, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = await db.CheckIns.AsNoTracking()
            .Where(c => c.Youth.Group == group && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .Select(c => c.YouthId)
            .Distinct()
            .ToListAsync(ct);
        return [.. ids];
    }

    /// <summary>How many different people in this group have checked in today.</summary>
    public async Task<int> GetTodayCountAsync(Group group, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns.AsNoTracking()
            .Where(c => c.Youth.Group == group && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .Select(c => c.YouthId)
            .Distinct()
            .CountAsync(ct);
    }

    /// <summary>This group's check-ins on a given (local) date, most recent first, with the person loaded.</summary>
    public async Task<List<CheckInRecord>> GetByDateAsync(DateOnly date, Group group, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(date);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns.AsNoTracking()
            .Include(c => c.Youth)
            .Where(c => c.Youth.Group == group && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .OrderByDescending(c => c.Timestamp)
            .ToListAsync(ct);
    }

    private static (DateTime UtcStart, DateTime UtcEnd) LocalDayRangeUtc(DateOnly localDate)
    {
        var localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local);
        return (utcStart, utcStart.AddDays(1));
    }
}
