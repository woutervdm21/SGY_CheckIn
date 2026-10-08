using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>Which people a list export covers.</summary>
public enum YouthListScope
{
    Active,
    Archived,
    All,
}

/// <summary>Narrows a Youth list export by leaders' notes.</summary>
public enum NotesFilter
{
    Everyone,
    TroubleMakers,
    CareVillage,
}

/// <summary>
/// Filters for the check-in history export. An empty <see cref="Grades"/> means every
/// grade. Round-trips through the download link's query string, so the page and the CSV
/// endpoint always agree on what's being exported. The group in the query string is only
/// checked against the signed-in user's (see Program.cs); the data always comes from theirs.
/// </summary>
public sealed record CheckInExportFilter(Group Group, DateOnly From, DateOnly To, IReadOnlySet<Grade> Grades, bool IncludeArchived)
{
    public string ToQuery() =>
        $"group={Group.ToKey()}&from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&grades={ExportQuery.FormatGrades(Grades)}&archived={IncludeArchived.ToString().ToLowerInvariant()}";

    public static CheckInExportFilter FromQuery(IQueryCollection query, Group group)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new CheckInExportFilter(
            group,
            ExportQuery.ParseDate(query["from"]) ?? today.AddDays(-84),
            ExportQuery.ParseDate(query["to"]) ?? today,
            ExportQuery.ParseGrades(query["grades"]),
            query["archived"] != "false");
    }
}

/// <summary>Filters for the people-list export. An empty <see cref="Grades"/> means every grade.</summary>
public sealed record YouthExportFilter(Group Group, YouthListScope Scope, IReadOnlySet<Grade> Grades, NotesFilter Notes)
{
    public string ToQuery() =>
        $"group={Group.ToKey()}&scope={Scope.ToString().ToLowerInvariant()}&grades={ExportQuery.FormatGrades(Grades)}&notes={Notes.ToString().ToLowerInvariant()}";

    public static YouthExportFilter FromQuery(IQueryCollection query, Group group) => new(
        group,
        Enum.TryParse<YouthListScope>(query["scope"], ignoreCase: true, out var scope) ? scope : YouthListScope.Active,
        ExportQuery.ParseGrades(query["grades"]),
        Enum.TryParse<NotesFilter>(query["notes"], ignoreCase: true, out var notes) ? notes : NotesFilter.Everyone);
}

internal static class ExportQuery
{
    public static string FormatGrades(IReadOnlySet<Grade> grades) =>
        string.Join(',', grades.Order().Select(g => (int)g));

    public static IReadOnlySet<Grade> ParseGrades(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => int.TryParse(v, out var n) ? (Grade?)n : null)
            .OfType<Grade>()
            .Where(Enum.IsDefined)
            .ToHashSet();

    public static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

/// <summary>
/// CSV exports for the admin Export page: check-in history (one row per arrival) and the
/// people list (one row per person), for the signed-in group only. Both carry every field
/// that group records, so the church office gets the whole record rather than a summary.
/// </summary>
/// <remarks>
/// Dates are written yyyy-MM-dd: spreadsheets read that the same way whatever their
/// region, where 02/10/2026 could become 10 February. The file starts with a UTF-8 byte
/// order mark so Excel shows accented names correctly.
/// </remarks>
public class ExportService(IDbContextFactory<AppDbContext> dbFactory)
{
    private sealed record Column(string Header, Func<Youth, string> Value);

    public async Task<int> CountCheckInsAsync(CheckInExportFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await CheckInsMatching(db, filter).CountAsync(ct);
    }

    public async Task<int> CountYouthAsync(YouthExportFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await YouthMatching(db, filter).CountAsync(ct);
    }

    public async Task<byte[]> CheckInHistoryCsvAsync(CheckInExportFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await CheckInsMatching(db, filter)
            .Include(c => c.Youth)
            .OrderBy(c => c.Timestamp)
            .ToListAsync(ct);

        var columns = PersonColumns(filter.Group);
        var csv = new CsvWriter();
        csv.Row(["Check-in ID", "Date", "Time", .. columns.Select(c => c.Header)]);
        foreach (var c in rows)
        {
            var local = ToLocal(c.Timestamp);
            csv.Row([
                c.Id.ToString(CultureInfo.InvariantCulture),
                local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                local.ToString("HH:mm", CultureInfo.InvariantCulture),
                .. columns.Select(col => col.Value(c.Youth)),
            ]);
        }
        return csv.ToBytes();
    }

    public async Task<byte[]> YouthListCsvAsync(YouthExportFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await YouthMatching(db, filter)
            .OrderBy(y => y.Surname).ThenBy(y => y.Name)
            .Select(y => new
            {
                Youth = y,
                Total = y.CheckIns.Count,
                First = y.CheckIns.Min(c => (DateTime?)c.Timestamp),
                Last = y.CheckIns.Max(c => (DateTime?)c.Timestamp),
            })
            .ToListAsync(ct);

        var columns = PersonColumns(filter.Group);
        var csv = new CsvWriter();
        csv.Row([.. columns.Select(c => c.Header), "Total check-ins", "First check-in", "Last check-in"]);
        foreach (var r in rows)
        {
            csv.Row([
                .. columns.Select(col => col.Value(r.Youth)),
                r.Total.ToString(CultureInfo.InvariantCulture),
                FormatDate(r.First),
                FormatDate(r.Last),
            ]);
        }
        return csv.ToBytes();
    }

    private static IQueryable<CheckInRecord> CheckInsMatching(AppDbContext db, CheckInExportFilter filter)
    {
        var (from, to) = filter.From <= filter.To ? (filter.From, filter.To) : (filter.To, filter.From);
        var utcStart = ToUtc(from);
        var utcEnd = ToUtc(to.AddDays(1));

        var query = db.CheckIns.AsNoTracking()
            .Where(c => c.Youth.Group == filter.Group && c.Timestamp >= utcStart && c.Timestamp < utcEnd);
        if (filter.Grades.Count > 0)
        {
            var grades = filter.Grades.Cast<Grade?>().ToList();
            query = query.Where(c => grades.Contains(c.Youth.Grade));
        }
        if (!filter.IncludeArchived)
        {
            query = query.Where(c => !c.Youth.IsArchived);
        }
        return query;
    }

    private static IQueryable<Youth> YouthMatching(AppDbContext db, YouthExportFilter filter)
    {
        var query = db.Youths.AsNoTracking().Where(y => y.Group == filter.Group);
        query = filter.Scope switch
        {
            YouthListScope.Active => query.Where(y => !y.IsArchived),
            YouthListScope.Archived => query.Where(y => y.IsArchived),
            _ => query,
        };
        if (filter.Grades.Count > 0)
        {
            var grades = filter.Grades.Cast<Grade?>().ToList();
            query = query.Where(y => grades.Contains(y.Grade));
        }
        return filter.Notes switch
        {
            NotesFilter.TroubleMakers => query.Where(y => y.BehaviourStatus == BehaviourStatus.Red),
            NotesFilter.CareVillage => query.Where(y => y.InCareVillage),
            _ => query,
        };
    }

    /// <summary>Every field the group records, in the order of the registration form.</summary>
    private static List<Column> PersonColumns(Group group)
    {
        List<Column> columns =
        [
            new("Person ID", y => y.Id.ToString(CultureInfo.InvariantCulture)),
            new("Name", y => y.Name),
            new("Surname", y => y.Surname),
        ];
        if (group.HasOwnCell())
        {
            columns.Add(new("Cell number", y => y.CellNo ?? ""));
        }
        if (group.HasGrades())
        {
            columns.Add(new("Grade", y => y.Grade.ToDisplayString()));
        }
        columns.Add(new("Date of birth", y => y.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        columns.Add(new("Age", y => AgeToday(y.DateOfBirth).ToString(CultureInfo.InvariantCulture)));
        if (group.HasMinistries())
        {
            columns.Add(new("Ministry", y => KidsMinistries.For(y.DateOfBirth, DateOnly.FromDateTime(DateTime.Today))?.ToDisplayString() ?? ""));
        }
        if (group.HasGender())
        {
            columns.Add(new("Boy or girl", y => y.Gender?.ToString() ?? ""));
        }
        if (group.HasParents())
        {
            columns.Add(new("Parent name", y => y.ParentName ?? ""));
            columns.Add(new("Parent surname", y => y.ParentSurname ?? ""));
            columns.Add(new("Parent cell number", y => y.ParentCellNo ?? ""));
        }
        if (group.HasMedical())
        {
            columns.Add(new("Medical & allergies", y => y.Medical ?? ""));
        }
        if (group.HasLeaderFlags())
        {
            columns.Add(new("Status", y => y.BehaviourStatus.ToDisplayString()));
        }
        if (group.HasCmr())
        {
            columns.Add(new("CMR", y => y.InCmr ? "Yes" : "No"));
        }
        if (group.HasCareVillage())
        {
            columns.Add(new("Care Village", y => y.InCareVillage ? "Yes" : "No"));
        }
        columns.Add(new("Comment", y => y.Comment ?? ""));
        columns.Add(new("Archived", y => y.IsArchived ? "Yes" : "No"));
        columns.Add(new("Registered on", y => FormatDate(y.CreatedAt)));
        return columns;
    }

    private static string FormatDate(DateTime? utc) =>
        utc is { } value ? ToLocal(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";

    private static int AgeToday(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        return dateOfBirth > today.AddYears(-age) ? age - 1 : age;
    }

    private static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local);

    private static DateTime ToUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), TimeZoneInfo.Local);

    private sealed class CsvWriter
    {
        private readonly StringBuilder sb = new();

        public void Row(IEnumerable<string> fields) =>
            sb.AppendJoin(',', fields.Select(Field)).Append("\r\n");

        public byte[] ToBytes() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];

        private static string Field(string value)
        {
            // A typed comment starting with = + - @ would run as a formula when the file is
            // opened in a spreadsheet; a leading apostrophe makes it plain text instead.
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                value = "'" + value;
            }

            return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }
}
