using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// Shared form model for both registering a new person and editing an existing one.
/// Validation lives here (rather than directly on <see cref="Youth"/>) so the entity
/// stays free of UI/validation concerns. Which fields are asked for depends on
/// <see cref="Group"/>; the ones a group doesn't use stay null and are saved as null.
/// </summary>
public class YouthFormModel : IValidatableObject
{
    /// <summary>The group the person is in. Set by the page, never by the form.</summary>
    public Group Group { get; set; } = Group.Youth;

    public bool AsksForCell => Group.HasOwnCell();
    public bool AsksForGrade => Group.HasGrades();
    public bool AsksForParent => Group.HasParents();
    public bool AsksForGender => Group.HasGender();

    /// <summary>
    /// Set by the register page: only a new child is turned away for being too young for
    /// Kids' Ministry, so children already on file can still be edited.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Ticked by the volunteer to register a child too old for Kids' Ministry anyway (an
    /// exception for today). Not saved: they're flagged again at every check-in.
    /// </summary>
    public bool TooOldException { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Surname is required")]
    [StringLength(100)]
    public string Surname { get; set; } = "";

    [RequiredInGroup(nameof(AsksForCell), ErrorMessage = "Cell number is required")]
    [Phone(ErrorMessage = "Enter a valid cell number")]
    [StringLength(20)]
    public string? CellNo { get; set; }

    [RequiredInGroup(nameof(AsksForGrade), ErrorMessage = "Grade is required")]
    public Grade? Grade { get; set; }

    [Required(ErrorMessage = "Date of birth is required")]
    public DateOnly? DateOfBirth { get; set; }

    [RequiredInGroup(nameof(AsksForGender), ErrorMessage = "Choose boy or girl")]
    public Gender? Gender { get; set; }

    [RequiredInGroup(nameof(AsksForParent), ErrorMessage = "Parent/guardian's name is required")]
    [StringLength(100)]
    public string? ParentName { get; set; }

    [RequiredInGroup(nameof(AsksForParent), ErrorMessage = "Parent/guardian's surname is required")]
    [StringLength(100)]
    public string? ParentSurname { get; set; }

    [RequiredInGroup(nameof(AsksForParent), ErrorMessage = "Parent/guardian's cell number is required")]
    [Phone(ErrorMessage = "Enter a valid cell number")]
    [StringLength(20)]
    public string? ParentCellNo { get; set; }

    /// <summary>Kids only; goes on the child's label. Optional — most kids have none.</summary>
    [StringLength(200, ErrorMessage = "Keep this under 200 characters so it fits on the label")]
    public string? Medical { get; set; }

    /// <summary>Leaders' note. Anyone signed in can edit it.</summary>
    [StringLength(2000, ErrorMessage = "Comment can't be longer than 2000 characters")]
    public string? Comment { get; set; }

    /// <summary>Admin-only behaviour flag; ignored from a volunteer's post — see <see cref="CopyTo"/>.</summary>
    public BehaviourStatus BehaviourStatus { get; set; } = BehaviourStatus.Green;

    /// <summary>Care Village flag, independent of <see cref="BehaviourStatus"/>. Anyone signed in can edit it.</summary>
    public bool InCareVillage { get; set; }

    /// <summary>Kids only. Anyone signed in can edit it.</summary>
    public bool InCmr { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateOfBirth is { } dob)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (dob > today)
            {
                yield return new ValidationResult(
                    "Date of birth can't be in the future",
                    [nameof(DateOfBirth)]);
            }
            else if (dob < today.AddYears(-100))
            {
                yield return new ValidationResult(
                    "That date of birth doesn't look right",
                    [nameof(DateOfBirth)]);
            }
            else if (IsNew && Group.HasMinistries() && KidsMinistries.For(dob, today) is null)
            {
                yield return new ValidationResult(
                    $"Your child isn't eligible for Kids' Ministry yet. They can join us in {KidsMinistries.JoiningYear(dob)}.",
                    [nameof(DateOfBirth)]);
            }
            else if (IsNew && Group.HasMinistries() && KidsMinistries.IsTooOld(dob, today) && !TooOldException)
            {
                yield return new ValidationResult(
                    "Too old for Kids' Ministry. Tick \"Make an exception today\" to register them anyway.",
                    [nameof(TooOldException)]);
            }
        }

        if (InCmr && InCareVillage && Group.HasCmr())
        {
            yield return new ValidationResult(
                "A child is either with CMR or in Care Village, not both",
                [nameof(InCmr)]);
        }

        if (AsksForGrade && Grade is { } grade && !Group.Grades().Contains(grade))
        {
            yield return new ValidationResult(
                $"Pick one of the {Group.ToDisplayString()} grades",
                [nameof(Grade)]);
        }
    }

    /// <summary>
    /// Copies the form onto <paramref name="youth"/>. Only the fields the person's group
    /// records are kept; the rest are cleared, so a hand-crafted post can't fill them in.
    /// </summary>
    /// <param name="includeBehaviourStatus">
    /// Whether to apply the admin-only <see cref="BehaviourStatus"/>. False for a
    /// volunteer: the input is hidden from their form, and a hand-crafted post must not be
    /// able to overwrite it.
    /// </param>
    public void CopyTo(Youth youth, bool includeBehaviourStatus)
    {
        var group = youth.Group;
        youth.Name = Name.Trim();
        youth.Surname = Surname.Trim();
        youth.CellNo = group.HasOwnCell() ? Clean(CellNo) : null;
        // A grade saved for a child before Kids went by ministry stays as it was.
        if (group != Group.Kids)
        {
            youth.Grade = group.HasGrades() ? Grade : null;
        }
        youth.Gender = group.HasGender() ? Gender : null;
        youth.DateOfBirth = DateOfBirth!.Value;

        var hasParents = group.HasParents();
        youth.ParentName = hasParents ? Clean(ParentName) : null;
        youth.ParentSurname = hasParents ? Clean(ParentSurname) : null;
        youth.ParentCellNo = hasParents ? Clean(ParentCellNo) : null;

        youth.Medical = group.HasMedical() ? Clean(Medical) : null;
        youth.Comment = Clean(Comment);

        youth.InCareVillage = group.HasCareVillage() && InCareVillage;
        youth.InCmr = group.HasCmr() && InCmr;
        if (group.HasLeaderFlags() && includeBehaviourStatus)
        {
            youth.BehaviourStatus = BehaviourStatus;
        }
    }

    public static YouthFormModel FromYouth(Youth youth) => new()
    {
        Group = youth.Group,
        Name = youth.Name,
        Surname = youth.Surname,
        CellNo = youth.CellNo,
        Grade = youth.Grade,
        DateOfBirth = youth.DateOfBirth,
        Gender = youth.Gender,
        ParentName = youth.ParentName,
        ParentSurname = youth.ParentSurname,
        ParentCellNo = youth.ParentCellNo,
        Medical = youth.Medical,
        Comment = youth.Comment,
        BehaviourStatus = youth.BehaviourStatus,
        InCareVillage = youth.InCareVillage,
        InCmr = youth.InCmr,
    };

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
