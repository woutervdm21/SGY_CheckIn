using System.Reflection;

namespace SGY.CheckIn;

/// <summary>
/// Exposes the app version (set once, in the csproj's &lt;Version&gt;) for display in the
/// UI — useful when reporting an issue with a specific deployed build.
/// </summary>
public static class AppInfo
{
    public static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";
}
