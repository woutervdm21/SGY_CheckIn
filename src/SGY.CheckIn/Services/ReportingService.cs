using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>One evening's attendance — a date the group actually met, and how many came.</summary>
public record SessionAttendance(DateOnly Date, int Count);

/// <summary>How many of the youth who came in the period were in each grade.</summary>
public record GradeAttendance(Grade Grade, int Count);

/// <summary>A youth and how many evenings they attended in the period.</summary>
public record YouthAttendance(int YouthId, string FullName, Grade Grade, int Evenings);

/// <summary>
/// A youth who didn't come in the period, with the last date they were seen at all
/// (null if they've never checked in since being registered).
/// </summary>
public record LapsedYouth(int YouthId, string FullName, Grade Grade, DateOnly? LastSeen);

/// <summary>Everything the admin dashboard shows for one date range.</summary>
public record AttendanceReport(
    DateOnly From,
    DateOnly To,
    int TotalCheckIns,
    int UniqueYouth,
    int NewRegistrations,
    IReadOnlyList<SessionAttendance> Sessions,
    IReadOnlyList<GradeAttendance> ByGrade,
    IReadOnlyList<YouthAttendance> TopAttendees,
    IReadOnlyList<LapsedYouth> Missing,
    int MissingTotal)
{
    /// <summary>Evenings the group actually met (dates with at least one check-in).</summary>
    public int SessionCount => Sessions.Count;

    /// <summary>
    /// Mean attendance of the evenings the group met. Averages the per-evening counts —
    /// not unique youth over sessions, which would answer a different (and much smaller)
    /// question now that the same youth attends many evenings.
    /// </summary>
    public double AveragePerSession => Sessions.Count == 0 ? 0 : Sessions.Average(s => s.Count);

    public int BusiestSession => Sessions.Count == 0 ? 0 : Sessions.Max(s => s.Count);
}

/// <summary>
/// Read-only aggregation over check-ins for the admin dashboard. Rows are pulled into
/// memory and grouped there rather than grouped in SQL: check-ins are stored in UTC but
/// every figure here is "per evening" in the church's local time zone, which SQLite can't
/// express, and a year of a youth group's attendance is a few thousand rows at most.
/// </summary>
public class ReportingService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<AttendanceReport> GetAttendanceAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var utcStart = ToUtc(from);
        var utcEnd = ToUtc(to.AddDays(1));

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.CheckIns.AsNoTracking()
            .Where(c => c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .Select(c => new { c.YouthId, c.Timestamp, c.Youth.Grade, c.Youth.Name, c.Youth.Surname })
            .ToListAsync(ct);

        var local = rows
            .Select(r => new
            {
                r.YouthId,
                r.Grade,
                FullName = $"{r.Name} {r.Surname}",
                Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(r.Timestamp, TimeZoneInfo.Local)),
            })
            .ToList();

        // Counted per evening by distinct youth: a youth scanned in twice on one night is
        // one attendee, not two, which is what a leader means by "how many came".
        var sessions = local
            .GroupBy(r => r.Date)
            .Select(g => new SessionAttendance(g.Key, g.Select(r => r.YouthId).Distinct().Count()))
            .OrderBy(s => s.Date)
            .ToList();

        var byGrade = local
            .GroupBy(r => r.Grade)
            .Select(g => new GradeAttendance(g.Key, g.Select(r => r.YouthId).Distinct().Count()))
            .OrderBy(g => g.Grade)
            .ToList();

        // Counted in evenings attended, not check-ins: that's what "came most" means, and
        // it makes the figure comparable to the number of evenings in the period.
        var topAttendees = local
            .GroupBy(r => r.YouthId)
            .Select(g => new YouthAttendance(
                g.Key,
                g.First().FullName,
                g.First().Grade,
                g.Select(r => r.Date).Distinct().Count()))
            .OrderByDescending(a => a.Evenings)
            .ThenBy(a => a.FullName)
            .Take(10)
            .ToList();

        var newRegistrations = await db.Youths.AsNoTracking()
            .CountAsync(y => y.CreatedAt >= utcStart && y.CreatedAt < utcEnd, ct);

        // Who's slipped away: youth still on the books who didn't come at all in the
        // period. "Last seen" looks at their whole history, not just the range, so the
        // list says how long they've been gone rather than only that they're absent.
        var attendedIds = local.Select(r => r.YouthId).Distinct().ToHashSet();

        var candidates = await db.Youths.AsNoTracking()
            .Where(y => !y.IsArchived)
            .Select(y => new
            {
                y.Id,
                y.Name,
                y.Surname,
                y.Grade,
                y.CreatedAt,
                LastCheckIn = (DateTime?)db.CheckIns
                    .Where(c => c.YouthId == y.Id)
                    .Max(c => (DateTime?)c.Timestamp),
            })
            .ToListAsync(ct);

        var missingAll = candidates
            .Where(y => !attendedIds.Contains(y.Id))
            .Select(y => new
            {
                Youth = new LapsedYouth(
                    y.Id,
                    $"{y.Name} {y.Surname}",
                    y.Grade,
                    y.LastCheckIn is { } seen
                        ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(seen, TimeZoneInfo.Local))
                        : null),
                // Never-attended youth sort by how long they've been registered, so a youth
                // who signed up months ago and never came ranks alongside a long absence.
                Effective = y.LastCheckIn ?? y.CreatedAt,
            })
            .OrderBy(x => x.Effective)
            .ToList();

        return new AttendanceReport(
            from,
            to,
            TotalCheckIns: local.Count,
            UniqueYouth: local.Select(r => r.YouthId).Distinct().Count(),
            NewRegistrations: newRegistrations,
            Sessions: sessions,
            ByGrade: byGrade,
            TopAttendees: topAttendees,
            Missing: [.. missingAll.Take(10).Select(x => x.Youth)],
            MissingTotal: missingAll.Count);
    }

    /// <summary>Flat per-check-in rows for the CSV export — one line per arrival.</summary>
    public async Task<List<(DateTime LocalTime, string Name, string Surname, Grade Grade)>> GetCheckInRowsAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var utcStart = ToUtc(from);
        var utcEnd = ToUtc(to.AddDays(1));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.CheckIns.AsNoTracking()
            .Where(c => c.Timestamp >= utcStart && c.Timestamp < utcEnd)
            .OrderBy(c => c.Timestamp)
            .Select(c => new { c.Timestamp, c.Youth.Name, c.Youth.Surname, c.Youth.Grade })
            .ToListAsync(ct);

        return [.. rows.Select(r => (
            TimeZoneInfo.ConvertTimeFromUtc(r.Timestamp, TimeZoneInfo.Local),
            r.Name,
            r.Surname,
            r.Grade))];
    }

    private static DateTime ToUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), TimeZoneInfo.Local);
}
