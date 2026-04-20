using Aspire.Hosting.Docker;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var registryEndpoint = builder.AddParameterFromConfiguration("registryEndpoint", "REGISTRY_ENDPOINT");
var registryRepository = builder.AddParameterFromConfiguration("registryRepository", "REGISTRY_REPOSITORY");

#pragma warning disable ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var registry = builder.AddContainerRegistry("ghcr", registryEndpoint, registryRepository);
#pragma warning restore ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#pragma warning disable ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPIPELINES003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var apiService = builder.AddProject<Projects.ArchiveTeam_Exporter_ApiService>("apiservice")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .PublishAsDockerComposeService((resource, service) => { service.Name = "exporter"; })
    .WithImagePushOptions(options =>
    {
        var endpointName = registryEndpoint.Resource.GetValueAsync(default).Result;
        var repositoryName = registryRepository.Resource.GetValueAsync(default).Result;
        options.Options.RemoteImageName = $"{endpointName}/{repositoryName}";
    })
    .WithContainerRegistry(registry);
#pragma warning restore ASPIREPIPELINES003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

builder.Build().Run();
