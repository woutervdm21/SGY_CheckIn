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

    /// <summary>
    /// Free-text leaders' note about this youth (allergies, pastoral concerns, anything the
    /// paper form had scribbled in the margin). Anyone signed in can edit it.
    /// </summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>
    /// Behaviour flag, green unless an admin raises it. Admin-only to edit — see
    /// <see cref="YouthFormModel.CopyTo"/>.
    /// </summary>
    public BehaviourStatus BehaviourStatus { get; set; } = BehaviourStatus.Green;

    /// <summary>
    /// Whether this youth is part of Care Village. Independent of
    /// <see cref="BehaviourStatus"/> — a youth can be flagged red and in Care Village at
    /// the same time — so it's its own flag rather than another status value. Anyone signed
    /// in can edit it.
    /// </summary>
    public bool InCareVillage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft-delete flag for youth who've left the group, so their historical check-ins
    /// stay intact instead of being deleted along with a hard-deleted record.
    /// </summary>
    public bool IsArchived { get; set; }

    public ICollection<CheckInRecord> CheckIns { get; set; } = new List<CheckInRecord>();

    public string FullName => $"{Name} {Surname}";
}
