using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// A youth group member. Holds the same details the paper registration form used to
/// capture: the youth's own details plus a parent/guardian contact.
/// </summary>
public class Youth
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [Required, MaxLength(100)]
    public string Surname { get; set; } = "";

    [Required, MaxLength(20)]
    public string CellNo { get; set; } = "";

    [Required]
    public Grade Grade { get; set; }

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(100)]
    public string ParentName { get; set; } = "";

    [Required, MaxLength(100)]
    public string ParentSurname { get; set; } = "";

    [Required, MaxLength(20)]
    public string ParentCellNo { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft-delete flag for youth who've left the group, so their historical check-ins
    /// stay intact instead of being deleted along with a hard-deleted record.
    /// </summary>
    public bool IsArchived { get; set; }

    public ICollection<CheckInRecord> CheckIns { get; set; } = new List<CheckInRecord>();

    public string FullName => $"{Name} {Surname}";
}
