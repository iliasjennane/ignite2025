using AgentCouncil.BlazorWasm.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using AgentCouncil.BlazorWasm;
using AgentCouncil.BlazorWasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Config: load optional demo-only settings
builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

// HttpClient for direct demo calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

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