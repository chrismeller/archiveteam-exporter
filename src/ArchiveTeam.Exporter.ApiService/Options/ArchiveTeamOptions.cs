using System.ComponentModel.DataAnnotations;

namespace ArchiveTeam.Exporter.ApiService.Options;

public class ArchiveTeamOptions
{
    public const string SectionName = "ArchiveTeam";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [ValidCommaSeparatedList]
    public string Projects { get; set; } = string.Empty;

    public int ProjectsCacheDurationMinutes { get; set; } = 30;

    public int StatsCacheDurationMinutes { get; set; } = 1;

    public TimeSpan ProjectsCacheDuration => TimeSpan.FromMinutes(ProjectsCacheDurationMinutes);

    public TimeSpan StatsCacheDuration => TimeSpan.FromMinutes(StatsCacheDurationMinutes);
}

public class ValidCommaSeparatedListAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace((string)value))
        {
            return new ValidationResult("Projects must contain at least one non-empty project name.");
        }

        var stringValue = (string)value;
        var parts = stringValue.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new ValidationResult("Projects must contain at least one non-empty project name.");
        }

        return ValidationResult.Success;
    }
}
