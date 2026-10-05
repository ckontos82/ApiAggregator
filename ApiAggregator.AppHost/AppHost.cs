var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ApiAggregator>("api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
