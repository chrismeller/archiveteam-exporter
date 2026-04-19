using System.Net;
using ArchiveTeam.Exporter.ApiService.Options;
using ArchiveTeam.Exporter.ApiService.Services;
using Microsoft.Extensions.Options;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var username = Environment.GetEnvironmentVariable("USERNAME");
var projectsWhitelist = Environment.GetEnvironmentVariable("PROJECTS_WHITELIST");

var inMemoryConfig = new Dictionary<string, string?>();

if (!string.IsNullOrEmpty(username))
{
    inMemoryConfig[$"{ArchiveTeamOptions.SectionName}:Username"] = username;
}

if (!string.IsNullOrEmpty(projectsWhitelist))
{
    inMemoryConfig[$"{ArchiveTeamOptions.SectionName}:ProjectsWhitelist"] = projectsWhitelist;
}

if (inMemoryConfig.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(inMemoryConfig);
}

builder.Services.Configure<ArchiveTeamOptions>(builder.Configuration.GetSection(ArchiveTeamOptions.SectionName));
builder.Services.AddOptions<ArchiveTeamOptions>()
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ProjectService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ArchiveTeam.Exporter/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.All
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapMetrics();

app.UseHttpMetrics();
app.MapGet("/metrics", async (ProjectService projectService, HttpContext context, CancellationToken cancellationToken) =>
{
    await projectService.GetProjectGaugesAsync(cancellationToken);
    var registry = Metrics.DefaultRegistry;
    var response = context.Response;
    response.ContentType = PrometheusConstants.TextContentTypeWithVersionAndEncoding;
    await registry.CollectAndExportAsTextAsync(response.Body, cancellationToken);
});

app.MapGet("/", () => "ArchiveTeam Exporter API. Navigate to /metrics for Prometheus metrics.");

app.MapDefaultEndpoints();

app.Run();
