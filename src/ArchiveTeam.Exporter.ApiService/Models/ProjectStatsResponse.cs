using System.Text.Json.Serialization;

namespace ArchiveTeam.Exporter.ApiService.Models;

public class ProjectStatsResponse
{
    [JsonPropertyName("downloaders")]
    public string[] Downloaders { get; set; } = [];

    [JsonPropertyName("downloader_bytes")]
    public Dictionary<string, double> DownloaderBytes { get; set; } = new();

    [JsonPropertyName("downloader_count")]
    public Dictionary<string, long> DownloaderCount { get; set; } = new();

    [JsonPropertyName("domain_bytes")]
    public DomainBytesInfo DomainBytes { get; set; } = new();

    [JsonPropertyName("total_items_todo")]
    public long TotalItemsTodo { get; set; }

    [JsonPropertyName("total_items_out")]
    public long TotalItemsOut { get; set; }

    [JsonPropertyName("total_items")]
    public long TotalItems { get; set; }

    [JsonPropertyName("counts")]
    public ProjectStatsCounts Counts { get; set; } = new();

    [JsonPropertyName("total_items_done")]
    public long TotalItemsDone { get; set; }
}

public class DomainBytesInfo
{
    [JsonPropertyName("data")]
    public double Data { get; set; }
}

public class ProjectStatsCounts
{
    [JsonPropertyName("done")]
    public long Done { get; set; }

    [JsonPropertyName("rcr")]
    public double Rcr { get; set; }

    [JsonPropertyName("out")]
    public long Out { get; set; }

    [JsonPropertyName("todo")]
    public long Todo { get; set; }
}
