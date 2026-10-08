using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// A member of one of the groups: Kids, Youth or Young Adults. Holds the same details the
/// paper registration form used to capture. Which details apply depends on the
/// <see cref="Group"/> (see <see cref="GroupExtensions"/>), so those columns are optional.
/// </summary>
/// <remarks>
/// Still called <c>Youth</c> (table <c>Youths</c>) from when the app only served the youth
/// group; renaming it would touch every file for no change in behaviour.
/// </remarks>
public class Youth
{
    public int Id { get; set; }

    /// <summary>Set when they're registered and never changed after.</summary>
    public Group Group { get; set; } = Group.Youth;

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [Required, MaxLength(100)]
    public string Surname { get; set; } = "";

    /// <summary>Their own cell number. Null for Kids, who are reached through their parent.</summary>
    [MaxLength(20)]
    public string? CellNo { get; set; }

    /// <summary>
    /// Youth only. Kids go by their ministry, worked out from their date of birth (see
    /// <see cref="KidsMinistries"/>); a grade saved for a child before that is kept but not
    /// shown. Null for young adults, who have left school.
    /// </summary>
    public Grade? Grade { get; set; }

    /// <summary>Kids only. Null for a child registered before it was asked.</summary>
    public Gender? Gender { get; set; }

    [Required]
    public DateOnly DateOfBirth { get; set; }

    // Parent/guardian contact: null for young adults, who are their own contact.
    [MaxLength(100)]
    public string? ParentName { get; set; }

    [MaxLength(100)]
    public string? ParentSurname { get; set; }

    [MaxLength(20)]
    public string? ParentCellNo { get; set; }

    /// <summary>
    /// Allergies and medical needs (asthma, epilepsy, medication), printed on a child's
    /// check-in label. Kids only. Kept apart from <see cref="Comment"/> so pastoral notes
    /// never end up stuck on a child.
    /// </summary>
    [MaxLength(200)]
    public string? Medical { get; set; }

    /// <summary>
    /// Free-text leaders' note (pastoral concerns, anything the paper form had scribbled in
    /// the margin, and allergies outside Kids). Anyone signed in can edit it. Never printed.
    /// </summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>
    /// Behaviour flag, green unless an admin raises it. Youth only. Admin-only to edit — see
    /// <see cref="YouthFormModel.CopyTo"/>.
    /// </summary>
    public BehaviourStatus BehaviourStatus { get; set; } = BehaviourStatus.Green;

    /// <summary>
    /// Whether this person is part of Care Village. Independent of
    /// <see cref="BehaviourStatus"/> — a youth can be flagged red and in Care Village at
    /// the same time — so it's its own flag rather than another status value. Kids and
    /// Youth. Anyone signed in can edit it.
    /// </summary>
    public bool InCareVillage { get; set; }

    /// <summary>
    /// Whether this child is with CMR. Kids only, and never together with
    /// <see cref="InCareVillage"/>. Like Care Village it shows on the child's label only as
    /// dots, which volunteers know to look for, so the label doesn't spell it out.
    /// </summary>
    public bool InCmr { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft-delete flag for youth who've left the group, so their historical check-ins
    /// stay intact instead of being deleted along with a hard-deleted record.
    /// </summary>
    public bool IsArchived { get; set; }

    public ICollection<CheckInRecord> CheckIns { get; set; } = new List<CheckInRecord>();

    public string FullName => $"{Name} {Surname}";
}
