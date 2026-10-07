using System.ComponentModel.DataAnnotations;

namespace SGY.CheckIn.Models;

/// <summary>
/// [Required], but only when the form's group records this field — e.g. a parent's name is
/// required for Kids and Youth and not asked for at all for young adults. Names a bool
/// property on the model that says whether the field applies.
/// </summary>
/// <remarks>
/// Derives from <see cref="RequiredAttribute"/> so the validator treats it as one: when it
/// fails, it's the only message shown, rather than also "Enter a valid cell number".
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredInGroupAttribute(string appliesProperty) : RequiredAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var applies = validationContext.ObjectType.GetProperty(appliesProperty)?
            .GetValue(validationContext.ObjectInstance) is true;
        return applies ? base.IsValid(value, validationContext) : ValidationResult.Success;
    }
}
