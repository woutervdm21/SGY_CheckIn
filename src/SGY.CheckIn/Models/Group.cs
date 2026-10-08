namespace SGY.CheckIn.Models;

/// <summary>
/// The church group a person belongs to. Everyone signs in to one group and only sees that
/// group's people, check-ins and reports. Stored as an int, so the values must not change.
/// </summary>
public enum Group
{
    Kids = 1,
    Youth = 2,
    YoungAdults = 3,
}

/// <summary>
/// What each group records and what it calls things. The forms, lists and exports ask these
/// rather than checking for a particular group, so the rules live in one place.
/// </summary>
public static class GroupExtensions
{
    /// <summary>The group's name in URLs and in the sign-in cookie.</summary>
    public static string ToKey(this Group group) => group switch
    {
        Group.Kids => "kids",
        Group.YoungAdults => "young-adults",
        _ => "youth",
    };

    public static Group? FromKey(string? key) => key switch
    {
        "kids" => Group.Kids,
        "youth" => Group.Youth,
        "young-adults" => Group.YoungAdults,
        _ => null,
    };

    public static string ToDisplayString(this Group group) => group switch
    {
        Group.Kids => "Kids",
        Group.YoungAdults => "Young Adults",
        _ => "Youth",
    };

    /// <summary>One member, in a sentence: "Register a new child".</summary>
    public static string PersonNoun(this Group group) => group switch
    {
        Group.Kids => "child",
        Group.YoungAdults => "young adult",
        _ => "youth",
    };

    /// <summary>Several members, in a sentence: "No kids registered yet".</summary>
    public static string PeopleNoun(this Group group) => group switch
    {
        Group.Kids => "kids",
        Group.YoungAdults => "young adults",
        _ => "youth",
    };

    /// <summary>At the start of a sentence or label: "Child details", "Kids in this period".</summary>
    public static string Capitalised(this string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];

    /// <summary>What one meeting of the group is called on the dashboard.</summary>
    public static string SessionNoun(this Group group) => group == Group.Youth ? "evening" : "session";

    /// <summary>
    /// Youth are known by their grade. Kids go by their ministry instead (from their date of
    /// birth, see <see cref="KidsMinistries"/>), and young adults have left school.
    /// </summary>
    public static bool HasGrades(this Group group) => group == Group.Youth;

    /// <summary>Kids are split into ministries by the year they were born.</summary>
    public static bool HasMinistries(this Group group) => group == Group.Kids;

    /// <summary>Kids record whether they're a boy or a girl.</summary>
    public static bool HasGender(this Group group) => group == Group.Kids;

    /// <summary>Kids and Youth can be in Care Village.</summary>
    public static bool HasCareVillage(this Group group) => group != Group.YoungAdults;

    /// <summary>Kids can be with CMR.</summary>
    public static bool HasCmr(this Group group) => group == Group.Kids;

    /// <summary>Kids and Youth have a parent/guardian on file; young adults are their own contact.</summary>
    public static bool HasParents(this Group group) => group != Group.YoungAdults;

    /// <summary>Kids are reached through their parent, so they have no cell number of their own.</summary>
    public static bool HasOwnCell(this Group group) => group != Group.Kids;

    /// <summary>Medical notes go on the label stuck to each child, so only Kids record them.</summary>
    public static bool HasMedical(this Group group) => group == Group.Kids;

    /// <summary>Only Kids print a name label at check-in.</summary>
    public static bool PrintsLabels(this Group group) => group == Group.Kids;

    /// <summary>Hint in the leaders' comment box. Kids' medical notes have their own field.</summary>
    public static string CommentPlaceholder(this Group group) => group.HasMedical()
        ? "Anything leaders should know — pastoral notes. Never printed."
        : "Anything leaders should know — allergies, pastoral notes.";

    /// <summary>
    /// The behaviour status is Youth only. Every group keeps the free-text leaders' comment.
    /// </summary>
    public static bool HasLeaderFlags(this Group group) => group == Group.Youth;

    /// <summary>The grades a member of this group can be in, youngest first. Empty for Kids and young adults.</summary>
    public static IReadOnlyList<Grade> Grades(this Group group) => group switch
    {
        Group.Youth => [Grade.Grade8, Grade.Grade9, Grade.Grade10, Grade.Grade11, Grade.Grade12],
        _ => [],
    };

    /// <summary>
    /// What a person is known by besides their name, in lists and on the check-in screen:
    /// "Grade 9" for Youth, "Bravehearts · 9" for Kids (or just the age, for a child too
    /// young for a ministry). Null for young adults.
    /// </summary>
    public static string? Describe(this Group group, Youth person, DateOnly today)
    {
        if (group.HasMinistries())
        {
            var age = Birthdays.AgeOn(person.DateOfBirth, today);
            return KidsMinistries.For(person.DateOfBirth, today) is { } ministry
                ? $"{ministry.ToDisplayString()} · {age}"
                : $"Age {age}";
        }
        return group.HasGrades() ? person.Grade?.ToDisplayString() : null;
    }
}
