using OilCaseX.Agent.Ui.Components;
using OilCaseX.Agent.Ui.Services;
using OilCaseX.Agent.Ui;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOptions<AgentUiOptions>()
    .BindConfiguration(AgentUiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<AgentHubConnection>();
builder.Services.AddScoped<AgentChatClientFallback>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
