using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// Shared form model for both registering a new youth and editing an existing one.
/// Validation lives here (rather than directly on <see cref="Youth"/>) so the entity
/// stays free of UI/validation concerns.
/// </summary>
public class YouthFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Surname is required")]
    [StringLength(100)]
    public string Surname { get; set; } = "";

    [Required(ErrorMessage = "Cell number is required")]
    [Phone(ErrorMessage = "Enter a valid cell number")]
    [StringLength(20)]
    public string CellNo { get; set; } = "";

    [Required(ErrorMessage = "Grade is required")]
    public Grade? Grade { get; set; }

    [Required(ErrorMessage = "Date of birth is required")]
    public DateOnly? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Parent/guardian's name is required")]
    [StringLength(100)]
    public string ParentName { get; set; } = "";

    [Required(ErrorMessage = "Parent/guardian's surname is required")]
    [StringLength(100)]
    public string ParentSurname { get; set; } = "";

    [Required(ErrorMessage = "Parent/guardian's cell number is required")]
    [Phone(ErrorMessage = "Enter a valid cell number")]
    [StringLength(20)]
    public string ParentCellNo { get; set; } = "";

    /// <summary>
    /// Admin-only note. Volunteers can open the edit form, so whatever arrives here from a
    /// volunteer's post is ignored — see <see cref="CopyTo"/>.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Comment can't be longer than 2000 characters")]
    public string? Comment { get; set; }

    /// <summary>Admin-only behaviour flag; ignored from a volunteer's post like <see cref="Comment"/>.</summary>
    public BehaviourStatus BehaviourStatus { get; set; } = BehaviourStatus.Green;

    /// <summary>Admin-only Care Village flag, independent of <see cref="BehaviourStatus"/>.</summary>
    public bool InCareVillage { get; set; }

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
        }
    }

    /// <param name="includeAdminFields">
    /// Whether to apply the admin-only fields (<see cref="Comment"/>,
    /// <see cref="BehaviourStatus"/> and <see cref="InCareVillage"/>). False for a
    /// volunteer: those fields are hidden from their form, and a hand-crafted post must
    /// not be able to overwrite them.
    /// </param>
    public void CopyTo(Youth youth, bool includeAdminFields)
    {
        youth.Name = Name.Trim();
        youth.Surname = Surname.Trim();
        youth.CellNo = CellNo.Trim();
        youth.Grade = Grade!.Value;
        youth.DateOfBirth = DateOfBirth!.Value;
        youth.ParentName = ParentName.Trim();
        youth.ParentSurname = ParentSurname.Trim();
        youth.ParentCellNo = ParentCellNo.Trim();

        if (includeAdminFields)
        {
            var comment = Comment?.Trim();
            youth.Comment = string.IsNullOrEmpty(comment) ? null : comment;
            youth.BehaviourStatus = BehaviourStatus;
            youth.InCareVillage = InCareVillage;
        }
    }

    public static YouthFormModel FromYouth(Youth youth) => new()
    {
        Name = youth.Name,
        Surname = youth.Surname,
        CellNo = youth.CellNo,
        Grade = youth.Grade,
        DateOfBirth = youth.DateOfBirth,
        ParentName = youth.ParentName,
        ParentSurname = youth.ParentSurname,
        ParentCellNo = youth.ParentCellNo,
        Comment = youth.Comment,
        BehaviourStatus = youth.BehaviourStatus,
        InCareVillage = youth.InCareVillage,
    };
}
