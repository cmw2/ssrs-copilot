var builder = DistributedApplication.CreateBuilder(args);

var agentService = builder.AddProject<Projects.SSRSCopilot_Agent>("agentservice")
    .WithHttpsHealthCheck("/health");

builder.AddProject<Projects.SSRSCopilot_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpsHealthCheck("/health")
    .WithReference(agentService)
    .WaitFor(agentService);

builder.Build().Run();
