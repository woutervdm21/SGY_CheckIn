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
    public async Task<CheckInRecord> CheckInAsync(int youthId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var record = new CheckInRecord { YouthId = youthId, Timestamp = DateTime.UtcNow };
        db.CheckIns.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    /// <summary>The most recent check-in for this youth today, if any — used to show
    /// an "already checked in" indicator without hard-blocking a re-check-in.</summary>
    public async Task<CheckInRecord?> GetTodayCheckInAsync(int youthId, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns.AsNoTracking()
            .Where(c => c.YouthId == youthId && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Youth IDs already checked in today, for annotating search results in bulk.</summary>
    public async Task<HashSet<int>> GetTodayCheckedInYouthIdsAsync(CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = await db.CheckIns.AsNoTracking()
            .Where(c => c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .Select(c => c.YouthId)
            .Distinct()
            .ToListAsync(ct);
        return [.. ids];
    }

    public async Task<int> GetTodayCountAsync(CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(DateOnly.FromDateTime(DateTime.Now));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns.AsNoTracking()
            .Where(c => c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .Select(c => c.YouthId)
            .Distinct()
            .CountAsync(ct);
    }

    /// <summary>All check-ins on a given (local) date, most recent first, with the youth loaded.</summary>
    public async Task<List<CheckInRecord>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = LocalDayRangeUtc(date);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CheckIns.AsNoTracking()
            .Include(c => c.Youth)
            .Where(c => c.Timestamp >= utcStart && c.Timestamp < utcEnd)
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
