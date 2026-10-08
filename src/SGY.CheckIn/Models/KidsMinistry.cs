namespace SGY.CheckIn.Models;

/// <summary>
/// The Kids ministries. A child's ministry comes from the year they were born, not their
/// grade: everyone born in the same year moves up together each January.
/// </summary>
public enum KidsMinistry
{
    JZone,
    Bravehearts,
    Refuel,
}

public static class KidsMinistries
{
    /// <summary>Kids' Ministry starts in the year a child turns this old.</summary>
    public const int YoungestAge = 5;

    /// <summary>Kids' Ministry ends after the year a child turns this old.</summary>
    public const int OldestAge = 13;

    /// <summary>
    /// The ministry for the age a child turns this year: J-Zone the years they turn 5 to 7,
    /// Bravehearts 8 to 11, Refuel 12 and 13. Null before the year they turn 5.
    /// </summary>
    /// <remarks>
    /// A child older than that (see <see cref="IsTooOld"/>) is flagged at registration and
    /// check-in; a leader can still make an exception for the day, and they print as Refuel.
    /// </remarks>
    public static KidsMinistry? For(DateOnly dateOfBirth, DateOnly today) =>
        (today.Year - dateOfBirth.Year) switch
        {
            < YoungestAge => null,
            <= 7 => KidsMinistry.JZone,
            <= 11 => KidsMinistry.Bravehearts,
            _ => KidsMinistry.Refuel,
        };

    /// <summary>Whether a child turns 14 or more this year, past Kids' Ministry.</summary>
    public static bool IsTooOld(DateOnly dateOfBirth, DateOnly today) =>
        today.Year - dateOfBirth.Year > OldestAge;

    /// <summary>The age a child turns this year.</summary>
    public static int AgeThisYear(DateOnly dateOfBirth, DateOnly today) => today.Year - dateOfBirth.Year;

    /// <summary>The year a child too young for Kids' Ministry can join.</summary>
    public static int JoiningYear(DateOnly dateOfBirth) => dateOfBirth.Year + YoungestAge;

    public static string ToDisplayString(this KidsMinistry ministry) => ministry switch
    {
        KidsMinistry.JZone => "J-Zone",
        KidsMinistry.Bravehearts => "Bravehearts",
        _ => "Refuel",
    };
}

/// <summary>Age and birthday sums shared by the check-in screen and the label.</summary>
public static class Birthdays
{
    public static int AgeOn(DateOnly dateOfBirth, DateOnly day)
    {
        var age = day.Year - dateOfBirth.Year;
        return dateOfBirth > day.AddYears(-age) ? age - 1 : age;
    }

    /// <summary>
    /// The birthday within a week either side of <paramref name="day"/>, or null if there's
    /// none. A 29 February birthday falls on the 28th in other years.
    /// </summary>
    public static DateOnly? NearbyBirthday(DateOnly dateOfBirth, DateOnly day)
    {
        // Last year's and next year's too, for birthdays across New Year.
        for (var year = day.Year - 1; year <= day.Year + 1; year++)
        {
            var birthday = new DateOnly(year, dateOfBirth.Month, Math.Min(dateOfBirth.Day, DateTime.DaysInMonth(year, dateOfBirth.Month)));
            if (Math.Abs(birthday.DayNumber - day.DayNumber) <= 7)
            {
                return birthday;
            }
        }
        return null;
    }
}
