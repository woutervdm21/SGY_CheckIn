using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// A key/value setting an admin can change from inside the app, so it doesn't need a
/// config edit and container restart: the volunteer passwords (see
/// <see cref="Auth.VolunteerPasswordStore"/>) and the Kids label style (see
/// <see cref="Services.LabelService"/>).
/// </summary>
public class AppSetting
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = "";

    public string? Value { get; set; }
}
