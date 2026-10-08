using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGY.CheckIn.Models;

/// <summary>
/// A single arrival record for a person. Kids are checked out too, when they're collected
/// (see <see cref="CheckOutCode"/>); the other groups only check in.
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

    /// <summary>
    /// Kids only: the code in the QR on the child's label. Scanning it opens the page that
    /// checks them out (/out/{code}). Random, so a code can't be guessed from another, and
    /// one per check-in, so last week's label can't check a child out today.
    /// </summary>
    [MaxLength(12)]
    public string? CheckOutCode { get; set; }

    /// <summary>Kids only: when the child was collected, in UTC. Null while they're still in.</summary>
    public DateTime? CheckedOutAt { get; set; }
}
