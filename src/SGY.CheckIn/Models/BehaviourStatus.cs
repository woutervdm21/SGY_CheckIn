namespace SGY.CheckIn.Models;

/// <summary>
/// Behaviour flag on a youth's profile. Everyone starts <see cref="Green"/>; an admin can
/// raise <see cref="Red"/> to mark a trouble maker so leaders know before Friday night.
/// An enum rather than a bool so an in-between level can be added without a data migration.
/// </summary>
public enum BehaviourStatus
{
    Green = 0,
    Red = 1,
}

public static class BehaviourStatusExtensions
{
    public static string ToDisplayString(this BehaviourStatus status) => status switch
    {
        BehaviourStatus.Red => "Trouble maker",
        _ => "No concerns",
    };

    /// <summary>CSS modifier used by the status dot/badge — see app.css.</summary>
    public static string ToCssModifier(this BehaviourStatus status) => status switch
    {
        BehaviourStatus.Red => "red",
        _ => "green",
    };
}
