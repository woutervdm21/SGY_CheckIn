namespace SGY.CheckIn.Models;

/// <summary>
/// School grade a youth is currently in. A closed set rather than free text, since this
/// gets searched/filtered and free text drifts ("Grade 8" vs "8" vs "gr8"). Limited to
/// grades 8–12 — the youth group is high-school only.
/// </summary>
public enum Grade
{
    Grade8 = 8,
    Grade9 = 9,
    Grade10 = 10,
    Grade11 = 11,
    Grade12 = 12,
}

public static class GradeExtensions
{
    public static string ToDisplayString(this Grade grade) => $"Grade {(int)grade}";
}
