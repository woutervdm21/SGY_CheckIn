namespace SGY.CheckIn.Models;

/// <summary>
/// Where a search term falls in a name: for ranking check-in results and highlighting the
/// part that was typed.
/// </summary>
public static class NameSearch
{
    /// <summary>
    /// The term's place in <paramref name="name"/>, ignoring case. At the start of a word if
    /// it's there ("an" in "Anke van Wyk" is the "An"), otherwise wherever it first appears
    /// ("ou" in "Wouter"). Null if it isn't in the name.
    /// </summary>
    public static (int Start, int Length)? Find(string name, string term)
    {
        term = term.Trim();
        if (term.Length == 0)
        {
            return null;
        }

        int? first = null;
        for (var i = name.IndexOf(term, StringComparison.OrdinalIgnoreCase); i >= 0;
             i = name.IndexOf(term, i + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (IsWordStart(name, i))
            {
                return (i, term.Length);
            }
            first ??= i;
        }
        return first is { } start ? (start, term.Length) : null;
    }

    /// <summary>
    /// How well the term matches, best first: 0 at the start of the name, 1 at the start of a
    /// later word, 2 inside a word (a missed first letter), 3 not at all.
    /// </summary>
    public static int Rank(string name, string term) => Find(name, term) switch
    {
        null => 3,
        { Start: 0 } => 0,
        { Start: var start } when IsWordStart(name, start) => 1,
        _ => 2,
    };

    // Surnames like "Ferreira-Steenkamp" start a word after the hyphen too.
    private static bool IsWordStart(string name, int index) =>
        index == 0 || name[index - 1] is ' ' or '-';
}
