using AgentCouncil.BlazorWasm.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using AgentCouncil.BlazorWasm;
using AgentCouncil.BlazorWasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Config: load optional demo-only settings from wwwroot
// Note: In Blazor WASM, config files in wwwroot are served as static files
// and loaded via HttpClient, so we don't use AddJsonFile here
// Configuration is loaded via appsettings.json in wwwroot which is automatically loaded

// HttpClient for direct demo calls with extended timeout for long-running AI agent requests
builder.Services.AddScoped(sp => 
{
    var client = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    client.Timeout = TimeSpan.FromMinutes(10); // Allow up to 10 minutes for AI agent responses
    return client;
});

// MudBlazor services
builder.Services.AddMudServices();

// Agent endpoints config
builder.Services.Configure<AgentEndpointsOptions>(options =>
	builder.Configuration.GetSection("AgentEndpoints").Bind(options));

// Register agent client - choose between API or Direct client
var useApiClient = builder.Configuration.GetValue<bool>("UseApiClient");
if (useApiClient)
{
    builder.Services.AddScoped<IAgentsClient, ApiAgentsClient>();
}
else
{
    builder.Services.AddScoped<IAgentsClient, DirectAgentsClient>();
}

// Register monitoring client
builder.Services.AddScoped<MonitoringClient>();

await builder.Build().RunAsync();