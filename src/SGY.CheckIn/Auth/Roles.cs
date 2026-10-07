namespace SGY.CheckIn.Auth;

/// <summary>
/// The two access levels, within the group someone signed in to (see <see cref="UserGroup"/>).
/// A volunteer can check people in and register new ones; an admin can additionally manage
/// (search/edit/archive) all profiles, and switch to another group without signing out.
/// </summary>
public static class Roles
{
    public const string Volunteer = "volunteer";
    public const string Admin = "admin";
}
