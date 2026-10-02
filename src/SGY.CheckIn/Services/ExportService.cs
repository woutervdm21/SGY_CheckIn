using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>Which youth a youth-list export covers.</summary>
public enum YouthListScope
{
    Active,
    Archived,
    All,
}

/// <summary>Narrows a youth-list export by leaders' notes.</summary>
public enum NotesFilter
{
    Everyone,
    TroubleMakers,
    CareVillage,
}

/// <summary>
/// Filters for the check-in history export. An empty <see cref="Grades"/> means every
/// grade. Round-trips through the download link's query string, so the page and the CSV
/// endpoint always agree on what's being exported.
/// </summary>
public sealed record CheckInExportFilter(DateOnly From, DateOnly To, IReadOnlySet<Grade> Grades, bool IncludeArchived)
{
    public string ToQuery() =>
        $"from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&grades={ExportQuery.FormatGrades(Grades)}&archived={IncludeArchived.ToString().ToLowerInvariant()}";

    public static CheckInExportFilter FromQuery(IQueryCollection query)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new CheckInExportFilter(
            ExportQuery.ParseDate(query["from"]) ?? today.AddDays(-84),
            ExportQuery.ParseDate(query["to"]) ?? today,
            ExportQuery.ParseGrades(query["grades"]),
            query["archived"] != "false");
    }
}

/// <summary>Filters for the youth-list export. An empty <see cref="Grades"/> means every grade.</summary>
public sealed record YouthExportFilter(YouthListScope Scope, IReadOnlySet<Grade> Grades, NotesFilter Notes)
{
    public string ToQuery() =>
        $"scope={Scope.ToString().ToLowerInvariant()}&grades={ExportQuery.FormatGrades(Grades)}&notes={Notes.ToString().ToLowerInvariant()}";

    public static YouthExportFilter FromQuery(IQueryCollection query) => new(
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
            .Select(v => int.TryParse(v, out var n) ? (Grade)n : default)
            .Where(Enum.IsDefined)
            .ToHashSet();

    public static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

/// <summary>
/// CSV exports for the admin Export page: check-in history (one row per arrival) and the
/// youth list (one row per youth). Both carry every stored field, so the church office
/// gets the whole record rather than a summary.
/// </summary>
/// <remarks>
/// Dates are written yyyy-MM-dd: spreadsheets read that the same way whatever their
/// region, where 02/10/2026 could become 10 February. The file starts with a UTF-8 byte
/// order mark so Excel shows accented names correctly.
/// </remarks>
public class ExportService(IDbContextFactory<AppDbContext> dbFactory)
{
    private static readonly string[] YouthColumns =
    [
        "Youth ID", "Name", "Surname", "Cell number", "Grade", "Date of birth", "Age",
        "Parent name", "Parent surname", "Parent cell number",
        "Status", "Care Village", "Comment", "Archived", "Registered on",
    ];

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

        var csv = new CsvWriter();
        csv.Row(["Check-in ID", "Date", "Time", .. YouthColumns]);
        foreach (var c in rows)
        {
            var local = ToLocal(c.Timestamp);
            csv.Row([
                c.Id.ToString(CultureInfo.InvariantCulture),
                local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                local.ToString("HH:mm", CultureInfo.InvariantCulture),
                .. YouthFields(c.Youth),
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

        var csv = new CsvWriter();
        csv.Row([.. YouthColumns, "Total check-ins", "First check-in", "Last check-in"]);
        foreach (var r in rows)
        {
            csv.Row([
                .. YouthFields(r.Youth),
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
            .Where(c => c.Timestamp >= utcStart && c.Timestamp < utcEnd);
        if (filter.Grades.Count > 0)
        {
            var grades = filter.Grades.ToList();
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
        var query = filter.Scope switch
        {
            YouthListScope.Active => db.Youths.AsNoTracking().Where(y => !y.IsArchived),
            YouthListScope.Archived => db.Youths.AsNoTracking().Where(y => y.IsArchived),
            _ => db.Youths.AsNoTracking(),
        };
        if (filter.Grades.Count > 0)
        {
            var grades = filter.Grades.ToList();
            query = query.Where(y => grades.Contains(y.Grade));
        }
        return filter.Notes switch
        {
            NotesFilter.TroubleMakers => query.Where(y => y.BehaviourStatus == BehaviourStatus.Red),
            NotesFilter.CareVillage => query.Where(y => y.InCareVillage),
            _ => query,
        };
    }

    private static string[] YouthFields(Youth y) =>
    [
        y.Id.ToString(CultureInfo.InvariantCulture),
        y.Name,
        y.Surname,
        y.CellNo,
        y.Grade.ToDisplayString(),
        y.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        AgeToday(y.DateOfBirth).ToString(CultureInfo.InvariantCulture),
        y.ParentName,
        y.ParentSurname,
        y.ParentCellNo,
        y.BehaviourStatus.ToDisplayString(),
        y.InCareVillage ? "Yes" : "No",
        y.Comment ?? "",
        y.IsArchived ? "Yes" : "No",
        FormatDate(y.CreatedAt),
    ];

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
