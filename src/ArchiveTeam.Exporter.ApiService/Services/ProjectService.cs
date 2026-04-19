using System.Diagnostics.Metrics;
using System.Text.Json;
using ArchiveTeam.Exporter.ApiService.Models;
using ArchiveTeam.Exporter.ApiService.Options;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _memoryCache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const string CacheKey = "whitelisted_projects";

    public ProjectService(
        HttpClient httpClient,
        IOptions<ArchiveTeamOptions> options,
        ILogger<ProjectService> logger,
        IMemoryCache memoryCache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _memoryCache = memoryCache;

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
        if (_memoryCache.TryGetValue(CacheKey, out ArchiveTeamProject[]? cachedProjects))
        {
            _logger.LogInformation("Cache hit: returning {Count} cached projects", cachedProjects?.Length ?? 0);
            return cachedProjects ?? [];
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(CacheKey, out cachedProjects))
            {
                _logger.LogInformation("Cache hit after lock: returning {Count} cached projects", cachedProjects?.Length ?? 0);
                return cachedProjects ?? [];
            }

            _logger.LogInformation("Cache miss: fetching projects from API");

            const string projectsUrl = "https://warriorhq.archiveteam.org/projects.json";

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

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_options.CacheDuration);

            _memoryCache.Set(CacheKey, whitelistedProjects, cacheOptions);

            _logger.LogInformation("Cached {Count} whitelisted projects for {CacheDuration}", whitelistedProjects.Length, _options.CacheDuration);

            return whitelistedProjects;
        }
        finally
        {
            _cacheLock.Release();
        }
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
