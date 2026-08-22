using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGY.CheckIn.Models;

/// <summary>
/// A single arrival record for a youth. Check-in only — no check-out/departure tracking
/// (out of scope for now).
/// </summary>
/// <remarks>
/// Named <c>CheckInRecord</c> rather than <c>CheckIn</c> to avoid colliding with the
/// project's own root namespace, <c>SGY.CheckIn</c>.
/// </remarks>
public class CheckInRecord
{
    public int Id { get; set; }

    [Required]
    public int YouthId { get; set; }

    [ForeignKey(nameof(YouthId))]
    public Youth Youth { get; set; } = null!;

    /// <summary>Stored in UTC; converted to local time only at display time to avoid DST bugs.</summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
