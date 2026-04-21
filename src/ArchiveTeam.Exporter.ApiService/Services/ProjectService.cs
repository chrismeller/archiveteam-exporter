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
    private readonly Gauge _projectTotalItemsGauge;
    private readonly Gauge _projectItemsDoneGauge;
    private readonly Gauge _projectItemsTodoGauge;
    private readonly Gauge _projectItemsOutGauge;
    private readonly Gauge _projectTotalBytesGauge;
    private readonly Gauge _projectUserBytesGauge;
    private readonly Gauge _projectUserItemsGauge;
    private readonly Gauge _cacheLastRefreshGauge;
    private readonly IMemoryCache _memoryCache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const string CacheKey = "whitelisted_projects";
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

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
                "Information about ArchiveTeam projects. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name", "title"]
                });

        _projectTotalItemsGauge = Metrics
            .CreateGauge(
                "archiveteam_project_total_items",
                "Total items in the project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name"]
                });

        _projectItemsDoneGauge = Metrics
            .CreateGauge(
                "archiveteam_project_items_done",
                "Completed items in the project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name"]
                });

        _projectItemsTodoGauge = Metrics
            .CreateGauge(
                "archiveteam_project_items_todo",
                "Remaining items to do in the project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name"]
                });

        _projectItemsOutGauge = Metrics
            .CreateGauge(
                "archiveteam_project_items_out",
                "Items currently being worked on in the project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name"]
                });

        _projectTotalBytesGauge = Metrics
            .CreateGauge(
                "archiveteam_project_total_bytes",
                "Total bytes processed by the project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name"]
                });

        _projectUserBytesGauge = Metrics
            .CreateGauge(
                "archiveteam_project_user_bytes",
                "Bytes downloaded by user for project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name", "username"]
                });

        _projectUserItemsGauge = Metrics
            .CreateGauge(
                "archiveteam_project_user_items",
                "Items downloaded by user for project. Data is cached; see archiveteam_cache_last_refresh_timestamp_seconds for cache age.",
                new GaugeConfiguration
                {
                    LabelNames = ["name", "username"]
                });

        _cacheLastRefreshGauge = Metrics
            .CreateGauge(
                "archiveteam_cache_last_refresh_timestamp_seconds",
                "Unix timestamp of the last successful data refresh from the ArchiveTeam API. Results are cached; this metric indicates when the cached data was last updated.",
                new GaugeConfiguration
                {
                    LabelNames = ["source"]
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
                .SetAbsoluteExpiration(_options.ProjectsCacheDuration);

            _memoryCache.Set(CacheKey, whitelistedProjects, cacheOptions);

            _logger.LogInformation("Cached {Count} whitelisted projects for {CacheDuration}", whitelistedProjects.Length, _options.ProjectsCacheDuration);

            _cacheLastRefreshGauge
                .WithLabels("projects")
                .Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

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

    public async Task<ProjectStatsResponse?> FetchProjectStatsAsync(string projectName, CancellationToken cancellationToken)
    {
        var cacheKey = $"stats_{projectName}";

        if (_memoryCache.TryGetValue(cacheKey, out ProjectStatsResponse? cachedStats))
        {
            _logger.LogInformation("Cache hit: returning cached stats for project {ProjectName}", projectName);
            return cachedStats;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cachedStats))
            {
                _logger.LogInformation("Cache hit after lock: returning cached stats for project {ProjectName}", projectName);
                return cachedStats;
            }

            _logger.LogInformation("Cache miss: fetching stats for project {ProjectName} from API", projectName);

            var statsUrl = $"https://v1.api.tracker.archiveteam.org/{projectName}/stats.json";

            var response = await _httpClient.GetAsync(statsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var statsResponse = await response.Content.ReadFromJsonAsync<ProjectStatsResponse>(_jsonSerializerOptions, cancellationToken);

            if (statsResponse == null)
            {
                _logger.LogWarning("Received null stats response for project {ProjectName}", projectName);
                return null;
            }

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_options.StatsCacheDuration);

            _memoryCache.Set(cacheKey, statsResponse, cacheOptions);

            _logger.LogInformation("Cached stats for project {ProjectName} for {CacheDuration}", projectName, _options.StatsCacheDuration);

            _cacheLastRefreshGauge
                .WithLabels("stats")
                .Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            return statsResponse;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<ArchiveTeamProject[]> GetProjectGaugesAsync(CancellationToken cancellationToken)
    {
        var projects = await FetchProjectsAsync(cancellationToken);

        foreach (var project in projects)
        {
            var name = SanitizeLabel(project.Name);
            var title = SanitizeLabel(project.Title);

            _projectInfoGauge
                .WithLabels(name, title)
                .Set(1);

            var stats = await FetchProjectStatsAsync(project.Name, cancellationToken);

            if (stats == null)
            {
                _logger.LogWarning("No stats available for project {ProjectName}", project.Name);
                continue;
            }

            _projectTotalItemsGauge
                .WithLabels(name)
                .Set(stats.TotalItems);

            _projectItemsDoneGauge
                .WithLabels(name)
                .Set(stats.TotalItemsDone);

            _projectItemsTodoGauge
                .WithLabels(name)
                .Set(stats.TotalItemsTodo);

            _projectItemsOutGauge
                .WithLabels(name)
                .Set(stats.TotalItemsOut);

            _projectTotalBytesGauge
                .WithLabels(name)
                .Set(stats.DomainBytes.Data);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                if (stats.DownloaderBytes.TryGetValue(_options.Username, out var userBytes))
                {
                    _projectUserBytesGauge
                        .WithLabels(name, SanitizeLabel(_options.Username))
                        .Set(userBytes);
                }

                if (stats.DownloaderCount.TryGetValue(_options.Username, out var userItems))
                {
                    _projectUserItemsGauge
                        .WithLabels(name, SanitizeLabel(_options.Username))
                        .Set(userItems);
                }
            }
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
