using ArchiveTeam.Exporter.ApiService.Models;

namespace ArchiveTeam.Exporter.ApiService.Services;

public interface IProjectService
{
    Task<ArchiveTeamProject[]> GetProjectGaugesAsync(CancellationToken cancellationToken);
    Task<ProjectStatsResponse?> FetchProjectStatsAsync(string projectName, CancellationToken cancellationToken);
}
