using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// A key/value setting an admin can change from inside the app, so it doesn't need a
/// config edit and container restart — currently just the volunteer password (see
/// <see cref="Auth.VolunteerPasswordStore"/>).
/// </summary>
public class AppSetting
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = "";

    public string? Value { get; set; }
}
