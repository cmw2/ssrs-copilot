var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.SSRSCopilot_ApiService>("apiservice")
    .WithHttpsHealthCheck("/health");

builder.AddProject<Projects.SSRSCopilot_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpsHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
