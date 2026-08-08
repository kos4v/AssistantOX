using OilCaseX.McpServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddOilCaseXHost()
    .AddOilCaseXApplicationServices()
    .AddOilCaseXApiClient()
    .AddOilCaseXObservability()
    .AddOilCaseXMcp();

var app = builder.Build();

app.UseOilCaseXMcpPipeline();

app.Run();