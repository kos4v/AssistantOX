using OilCaseX.Agent.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddOilCaseXAgentHost()
    .AddOilCaseXAgentConfiguration()
    .AddOilCaseXAgentAuthentication()
    .AddOilCaseXAgentApplicationServices()
    .AddOilCaseXAgentObservability();

var app = builder.Build();

app.UseOilCaseXAgentPipeline();

app.Run();

public partial class Program;
