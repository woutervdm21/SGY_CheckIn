namespace SGY.CheckIn.Models;

/// <summary>
/// School grade a child or youth is currently in. A closed set rather than free text, since
/// this gets searched/filtered and free text drifts ("Grade 8" vs "8" vs "gr8"). Kids are
/// Grade RRR to 7 and Youth 8 to 12 — see <see cref="GroupExtensions.Grades"/>. Stored as
/// an int, so RRR, RR and R sit below Grade 1 and grades sort in school order.
/// </summary>
public enum Grade
{
    GradeRRR = -2,
    GradeRR = -1,
    GradeR = 0,
    Grade1 = 1,
    Grade2 = 2,
    Grade3 = 3,
    Grade4 = 4,
    Grade5 = 5,
    Grade6 = 6,
    Grade7 = 7,
    Grade8 = 8,
    Grade9 = 9,
    Grade10 = 10,
    Grade11 = 11,
    Grade12 = 12,
}

public static class GradeExtensions
{
    public static string ToDisplayString(this Grade grade) => grade switch
    {
        Grade.GradeRRR => "Grade RRR",
        Grade.GradeRR => "Grade RR",
        Grade.GradeR => "Grade R",
        _ => $"Grade {(int)grade}",
    };

    /// <summary>Blank for someone with no grade (a young adult).</summary>
    public static string ToDisplayString(this Grade? grade) => grade is { } g ? g.ToDisplayString() : "";
}
