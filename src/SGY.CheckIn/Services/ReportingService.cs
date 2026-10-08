using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>One evening's attendance — a date the group actually met, and how many came.</summary>
public record SessionAttendance(DateOnly Date, int Count);

/// <summary>How many of the youth who came in the period were in each grade.</summary>
public record GradeAttendance(Grade Grade, int Count);

/// <summary>A youth and how many evenings they attended in the period.</summary>
public record YouthAttendance(int YouthId, string FullName, Grade? Grade, int Evenings);

/// <summary>
/// A youth who didn't come in the period, with the last date they were seen at all
/// (null if they've never checked in since being registered).
/// </summary>
public record LapsedYouth(int YouthId, string FullName, Grade? Grade, DateOnly? LastSeen);

/// <summary>A birthday coming up: the day, and the age they turn.</summary>
public record UpcomingBirthday(int YouthId, string FullName, DateOnly Birthday, int Turns);

/// <summary>Someone on the books the admin might archive, and why.</summary>
public record ArchiveSuggestion(int YouthId, string FullName, IReadOnlyList<string> Reasons);

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
/// Read-only aggregation over one group's check-ins for the admin dashboard. Rows are pulled into
/// memory and grouped there rather than grouped in SQL: check-ins are stored in UTC but
/// every figure here is "per evening" in the church's local time zone, which SQLite can't
/// express, and a year of a youth group's attendance is a few thousand rows at most.
/// </summary>
public class ReportingService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<AttendanceReport> GetAttendanceAsync(DateOnly from, DateOnly to, Group group, CancellationToken ct = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var utcStart = ToUtc(from);
        var utcEnd = ToUtc(to.AddDays(1));

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.CheckIns.AsNoTracking()
            .Where(c => c.Youth.Group == group && c.Timestamp >= utcStart && c.Timestamp < utcEnd)
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

        // Young adults have no grade, so their breakdown comes out empty.
        var byGrade = local
            .Where(r => r.Grade is not null)
            .GroupBy(r => r.Grade!.Value)
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
            .CountAsync(y => y.Group == group && y.CreatedAt >= utcStart && y.CreatedAt < utcEnd, ct);

        // Who's slipped away: youth still on the books who didn't come at all in the
        // period. "Last seen" looks at their whole history, not just the range, so the
        // list says how long they've been gone rather than only that they're absent.
        var attendedIds = local.Select(r => r.YouthId).Distinct().ToHashSet();

        var candidates = await db.Youths.AsNoTracking()
            .Where(y => y.Group == group && !y.IsArchived)
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

    /// <summary>
    /// Birthdays from today to a week from today, soonest first, for the leaders to plan a
    /// fuss. Archived people are left out.
    /// </summary>
    public async Task<IReadOnlyList<UpcomingBirthday>> GetUpcomingBirthdaysAsync(Group group, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var people = await db.Youths.AsNoTracking()
            .Where(y => y.Group == group && !y.IsArchived)
            .Select(y => new { y.Id, y.Name, y.Surname, y.DateOfBirth })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        return [.. people
            .Select(p => (Person: p, Birthday: Birthdays.NearbyBirthday(p.DateOfBirth, today)))
            .Where(x => x.Birthday is { } day && day >= today)
            .Select(x => new UpcomingBirthday(x.Person.Id, $"{x.Person.Name} {x.Person.Surname}", x.Birthday!.Value, x.Birthday.Value.Year - x.Person.DateOfBirth.Year))
            .OrderBy(b => b.Birthday)];
    }

    /// <summary>A year away counts as gone.</summary>
    public const int AwayDays = 365;

    /// <summary>Youth ends after the year they turn 18 (matric).</summary>
    public const int YouthOldestAge = 18;

    /// <summary>
    /// People still on the books who've probably left: not seen for over a year (or
    /// registered over a year ago and never seen), or too old for Kids or Youth. Archiving
    /// stays the admin's call; this only points them out. Longest gone first.
    /// </summary>
    public async Task<IReadOnlyList<ArchiveSuggestion>> GetArchiveSuggestionsAsync(Group group, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var people = await db.Youths.AsNoTracking()
            .Where(y => y.Group == group && !y.IsArchived)
            .Select(y => new
            {
                y.Id,
                y.Name,
                y.Surname,
                y.DateOfBirth,
                y.CreatedAt,
                LastCheckIn = (DateTime?)db.CheckIns
                    .Where(c => c.YouthId == y.Id)
                    .Max(c => (DateTime?)c.Timestamp),
            })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var cutoff = DateTime.UtcNow.AddDays(-AwayDays);
        var suggestions = new List<(ArchiveSuggestion Suggestion, DateTime Since)>();
        foreach (var p in people)
        {
            List<string> reasons = [];
            if (p.LastCheckIn is { } seen && seen < cutoff)
            {
                reasons.Add($"Not seen for over a year (last on {ToLocalDate(seen):d MMM yyyy})");
            }
            else if (p.LastCheckIn is null && p.CreatedAt < cutoff)
            {
                reasons.Add($"Never checked in since registering on {ToLocalDate(p.CreatedAt):d MMM yyyy}");
            }

            var turns = KidsMinistries.AgeThisYear(p.DateOfBirth, today);
            if (group == Group.Kids && KidsMinistries.IsTooOld(p.DateOfBirth, today))
            {
                reasons.Add($"Turns {turns} this year: too old for Kids, which ends at {KidsMinistries.OldestAge}");
            }
            else if (group == Group.Youth && turns > YouthOldestAge)
            {
                reasons.Add($"Turns {turns} this year: past school age, too old for Youth");
            }

            if (reasons.Count > 0)
            {
                suggestions.Add((new ArchiveSuggestion(p.Id, $"{p.Name} {p.Surname}", reasons), p.LastCheckIn ?? p.CreatedAt));
            }
        }
        return [.. suggestions.OrderBy(s => s.Since).Select(s => s.Suggestion)];
    }

    private static DateOnly ToLocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local));

    private static DateTime ToUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), TimeZoneInfo.Local);
}
