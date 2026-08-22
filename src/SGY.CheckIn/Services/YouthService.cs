using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>
/// Search, registration, and editing of youth records. Kept deliberately simple for a
/// dataset in the tens-to-low-hundreds — no external search index needed.
/// </summary>
public class YouthService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>
    /// Live search by name/surname for the search-as-you-type box. Capped at 25 results,
    /// which is plenty for a youth group's scale and keeps the results list scannable.
    /// </summary>
    public async Task<List<Youth>> SearchAsync(string term, CancellationToken ct = default)
    {
        term = term.Trim();
        if (term.Length == 0)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var pattern = $"%{term}%";
        return await db.Youths.AsNoTracking()
            .Where(y => !y.IsArchived &&
                (EF.Functions.Like(y.Name, pattern) ||
                 EF.Functions.Like(y.Surname, pattern) ||
                 EF.Functions.Like(y.Name + " " + y.Surname, pattern)))
            .OrderBy(y => y.Surname).ThenBy(y => y.Name)
            .Take(25)
            .ToListAsync(ct);
    }

    public async Task<Youth?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Youths.AsNoTracking().FirstOrDefaultAsync(y => y.Id == id, ct);
    }

    /// <summary>
    /// Loose duplicate check used as a non-blocking nudge during registration: same
    /// surname (case-insensitive) plus a name that's an exact match or shares its first
    /// few letters. Never a hard block — two kids can legitimately share a name.
    /// </summary>
    public async Task<List<Youth>> FindSimilarAsync(string name, string surname, CancellationToken ct = default)
    {
        name = name.Trim();
        surname = surname.Trim();
        if (name.Length == 0 || surname.Length == 0)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var namePrefix = name.Length >= 3 ? name[..3] : name;
        var namePrefixPattern = $"{namePrefix}%";

        return await db.Youths.AsNoTracking()
            .Where(y => !y.IsArchived && y.Surname.ToLower() == surname.ToLower())
            .Where(y => EF.Functions.Like(y.Name, namePrefixPattern) || y.Name.ToLower() == name.ToLower())
            .ToListAsync(ct);
    }

    /// <summary>Creates a new youth record and returns it (with its assigned Id).</summary>
    public async Task<Youth> CreateAsync(YouthFormModel form, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var youth = new Youth();
        form.CopyTo(youth);
        db.Youths.Add(youth);
        await db.SaveChangesAsync(ct);
        return youth;
    }

    public async Task UpdateAsync(int id, YouthFormModel form, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var youth = await db.Youths.FirstOrDefaultAsync(y => y.Id == id, ct)
            ?? throw new InvalidOperationException($"Youth {id} not found.");
        form.CopyTo(youth);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Soft-deletes a youth (e.g. they've left the group) while preserving their check-in history.</summary>
    public async Task ArchiveAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var youth = await db.Youths.FirstOrDefaultAsync(y => y.Id == id, ct)
            ?? throw new InvalidOperationException($"Youth {id} not found.");
        youth.IsArchived = true;
        await db.SaveChangesAsync(ct);
    }
}
