namespace SGY.CheckIn.Auth;

/// <summary>
/// The two access levels. A volunteer can check youth in and register new ones; an admin
/// can additionally manage (search/edit/archive) all youth profiles.
/// </summary>
public static class Roles
{
    public const string Volunteer = "volunteer";
    public const string Admin = "admin";
}
