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

    public void CopyTo(Youth youth)
    {
        youth.Name = Name.Trim();
        youth.Surname = Surname.Trim();
        youth.CellNo = CellNo.Trim();
        youth.Grade = Grade!.Value;
        youth.DateOfBirth = DateOfBirth!.Value;
        youth.ParentName = ParentName.Trim();
        youth.ParentSurname = ParentSurname.Trim();
        youth.ParentCellNo = ParentCellNo.Trim();
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
    };
}
