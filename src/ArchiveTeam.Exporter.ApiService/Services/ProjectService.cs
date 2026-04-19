using System.Diagnostics.Metrics;
using System.Text.Json;
using ArchiveTeam.Exporter.ApiService.Models;
using ArchiveTeam.Exporter.ApiService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace ArchiveTeam.Exporter.ApiService.Services;

public class ProjectService : IProjectService
{
    private readonly HttpClient _httpClient;
    private readonly ArchiveTeamOptions _options;
    private readonly ILogger<ProjectService> _logger;
    private readonly Gauge _projectInfoGauge;

    public ProjectService(
        HttpClient httpClient,
        IOptions<ArchiveTeamOptions> options,
        ILogger<ProjectService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _projectInfoGauge = Metrics
            .CreateGauge(
                "archiveteam_projects_info",
                "Information about ArchiveTeam projects",
                new GaugeConfiguration
                {
                    LabelNames = ["name", "title", "description"]
                });
    }

    public async Task<ArchiveTeamProject[]> FetchProjectsAsync(CancellationToken cancellationToken)
    {
        const string projectsUrl = "https://warriorhq.archiveteam.org/projects.json";

        _logger.LogInformation("Fetching projects from {ProjectsUrl}", projectsUrl);

        var response = await _httpClient.GetAsync(projectsUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var projectsResponse = await response.Content.ReadFromJsonAsync<ArchiveTeamProjectsResponse>(cancellationToken);

        var projects = projectsResponse?.Projects ?? [];

        _logger.LogInformation("Successfully loaded {Count} projects", projects.Length);

        var whitelistedProjects = FilterByWhitelist(projects);

        if (whitelistedProjects.Length < projects.Length)
        {
            _logger.LogInformation("Filtered to {WhitelistCount} projects based on whitelist", whitelistedProjects.Length);
        }

        return whitelistedProjects;
    }

    private ArchiveTeamProject[] FilterByWhitelist(ArchiveTeamProject[] projects)
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectsWhitelist))
        {
            return projects;
        }

        var whitelistEntries = _options.ProjectsWhitelist.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var whitelistSet = whitelistEntries.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return projects
            .Where(p => whitelistSet.Contains(p.Name))
            .ToArray();
    }

    public async Task<ArchiveTeamProject[]> GetProjectGaugesAsync(CancellationToken cancellationToken)
    {
        var projects = await FetchProjectsAsync(cancellationToken);

        foreach (var project in projects)
        {
            var name = SanitizeLabel(project.Name);
            var title = SanitizeLabel(project.Title);
            var description = SanitizeLabel(project.Description);

            _projectInfoGauge
                .WithLabels(name, title, description)
                .Set(1);
        }

        return projects;
    }

    private static string SanitizeLabel(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return new string(value
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .ToArray());
    }
}
