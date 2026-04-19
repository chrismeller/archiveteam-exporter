using System.ComponentModel.DataAnnotations;

namespace ArchiveTeam.Exporter.ApiService.Options;

public class ArchiveTeamOptions
{
    public const string SectionName = "ArchiveTeam";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; set; } = string.Empty;

    [ValidCommaSeparatedList]
    public string ProjectsWhitelist { get; set; } = string.Empty;
}

public class ValidCommaSeparatedListAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace((string)value))
        {
            return ValidationResult.Success;
        }

        var stringValue = (string)value;
        var parts = stringValue.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new ValidationResult("ProjectsWhitelist must contain at least one non-empty project name.");
        }

        return ValidationResult.Success;
    }
}
