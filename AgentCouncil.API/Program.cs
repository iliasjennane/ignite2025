using AgentCouncil.API.Services;
using System.Text.Json;
using Scalar.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Azure.Monitor.OpenTelemetry.Exporter;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry with Azure Monitor (conditional)
var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) && 
                                !connectionString.StartsWith(";");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("AgentCouncil.Agents");
        
        // Only add Azure Monitor exporter if connection string is valid
        if (hasValidConnectionString)
        {
            tracing.AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = connectionString;
            });
        }
        else
        {
            tracing.AddConsoleExporter(); // Fallback to console in development
        }
    });

// Create additional tracer provider for Azure AI Projects (conditional)
TracerProvider? tracerProvider = null;
if (hasValidConnectionString)
{
    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource("Azure.AI")
        .AddSource("AgentCouncil.Agents")
        .AddAzureMonitorTraceExporter(o =>
        {
            o.ConnectionString = connectionString;
        })
        .AddConsoleExporter() // Keep for development debugging
        .Build();
}
else
{
    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource("Azure.AI")
        .AddSource("AgentCouncil.Agents")
        .AddConsoleExporter()
        .Build();
}

// Add services to the container
builder.Services.AddSingleton<FoundryAgentProvider>();
builder.Services.AddSingleton<AgUiService>(); // NEW: AG UI service for CopilotKit
builder.Services.AddHttpClient(); // Add HttpClient support
builder.Services.AddSingleton<TelemetryQueryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
// builder.Services.AddSwaggerGen(); // Commented out due to compatibility issues

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
              "http://localhost:5033", 
              "http://localhost:5002", 
              "http://localhost:5001", 
              "http://localhost:5000",
              "http://localhost:3000",    // NEW: CopilotKit UI
              "https://localhost:3000"    // NEW: CopilotKit UI HTTPS
          )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Log Application Insights configuration status
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (hasValidConnectionString)
{
    logger.LogInformation("Application Insights configured with connection string length: {Length}", 
        connectionString?.Length ?? 0);
}
else
{
    logger.LogWarning("Application Insights not configured (empty or invalid connection string). Using console exporter.");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors("AllowAll");
// app.UseHttpsRedirection(); // Disabled - using HTTP only

// Define route group for agent endpoints
var agentsGroup = app.MapGroup("/api/agents");

// POST endpoint for each agent
agentsGroup.MapPost("/{agentName}/chat", async (
    string agentName,
    ChatRequest request,
    FoundryAgentProvider provider,
    ILogger<Program> logger) =>
{
    // Validate the request
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Message cannot be empty" });
    }

    if (string.IsNullOrWhiteSpace(agentName))
    {
        return Results.BadRequest(new { error = "Agent name cannot be empty" });
    }

    try
    {
        var requestStartTime = DateTime.UtcNow;
        logger.LogInformation("⏱️ [PERF] Request START for agent {AgentName}: {Message}", agentName, request.Message);
        
        var (responseText, toolsUsed, connectedAgents) = await provider.SendAsync(agentName, request.Message);
        
        var requestDuration = DateTime.UtcNow - requestStartTime;
        logger.LogInformation("⏱️ [PERF] Request COMPLETE for agent {AgentName} - Total duration: {Duration}ms ({DurationSeconds:F2}s)", 
            agentName, requestDuration.TotalMilliseconds, requestDuration.TotalSeconds);
        
        var result = Results.Ok(new { 
            reply = responseText,
            toolsUsed = toolsUsed,
            connectedAgents = connectedAgents,
            durationMs = requestDuration.TotalMilliseconds
        });
        return result;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing chat request for agent {AgentName}", agentName);
        return Results.Problem($"Error processing chat: {ex.Message}");
    }
})
.WithName("ChatWithAgent");

// GET endpoint to list available agents
agentsGroup.MapGet("/", async (FoundryAgentProvider provider, ILogger<Program> logger) =>
{
    try
    {
        var agents = new[]
        {
            "chief_analyst",
            "sales_insights",
            "dealer_performance",
            "inventory_ops"
        };

        var agentInfo = new List<object>();
        foreach (var agentName in agents)
        {
            try
            {
                       var agent = await provider.GetAgentInfoAsync(agentName);
                var threadId = await provider.GetThreadIdAsync(agentName);
                
                // Use dynamic to access properties of the anonymous object
                dynamic agentObj = agent;
                agentInfo.Add(new
                {
                    name = agentName,
                    id = agentObj.Id,
                    threadId = threadId,
                    model = agentObj.Model,
                    description = agentObj.Description
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get info for agent {AgentName}", agentName);
                agentInfo.Add(new
                {
                    name = agentName,
                    id = "unknown",
                    threadId = (string?)null,
                    model = "unknown",
                    description = "Agent not available"
                });
            }
        }

        return Results.Ok(new { agents = agentInfo });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error listing agents");
        return Results.Problem($"Error listing agents: {ex.Message}");
    }
})
.WithName("ListAgents");

// Define route group for monitoring endpoints
var monitoringGroup = app.MapGroup("/api/monitoring");

// GET endpoint for recent traces
monitoringGroup.MapGet("/traces", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger,
    int count = 50,
    string? agentName = null) =>
{
    try
    {
        var traces = await telemetryService.GetRecentTracesAsync(count, agentName);
        return Results.Ok(new { traces });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving traces");
        return Results.Problem($"Error retrieving traces: {ex.Message}");
    }
})
.WithName("GetTraces");

// GET endpoint for agent metrics
monitoringGroup.MapGet("/metrics/{agentName}", async (
    string agentName,
    TelemetryQueryService telemetryService,
    ILogger<Program> logger,
    int hours = 24) =>
{
    try
    {
        var metrics = await telemetryService.GetAgentMetricsAsync(agentName, hours);
        return Results.Ok(metrics);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving metrics for agent {AgentName}", agentName);
        return Results.Problem($"Error retrieving metrics: {ex.Message}");
    }
})
.WithName("GetAgentMetrics");

// GET endpoint for recent errors
monitoringGroup.MapGet("/errors", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger,
    int count = 20) =>
{
    try
    {
        var errors = await telemetryService.GetRecentErrorsAsync(count);
        return Results.Ok(new { errors });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving errors");
        return Results.Problem($"Error retrieving errors: {ex.Message}");
    }
})
.WithName("GetErrors");

// GET endpoint for dashboard summary
monitoringGroup.MapGet("/summary", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger) =>
{
    try
    {
        var summary = await telemetryService.GetDashboardSummaryAsync();
        return Results.Ok(summary);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving dashboard summary");
        return Results.Problem($"Error retrieving summary: {ex.Message}");
    }
})
.WithName("GetDashboardSummary");

// 🧭 Query 1: Agent and Model Usage
monitoringGroup.MapGet("/agent-usage", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger) =>
{
    try
    {
        var data = await telemetryService.GetAgentModelUsageAsync();
        return Results.Ok(new { title = "Agent * Model Usage", data });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving agent and model usage");
        return Results.Problem($"Error retrieving agent and model usage: {ex.Message}");
    }
})
.WithName("GetAgentUsage");

// 🧮 Query 2: Agent Complexity Index
monitoringGroup.MapGet("/tool-usage", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger) =>
{
    try
    {
        var data = await telemetryService.GetAgentComplexityAsync();
        return Results.Ok(new { title = "Agent Complexity Index", data });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving agent complexity");
        return Results.Problem($"Error retrieving agent complexity: {ex.Message}");
    }
})
.WithName("GetToolUsage");

// 🚦 Query 4: Success vs. failure rate
monitoringGroup.MapGet("/success-rate", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger) =>
{
    try
    {
        var data = await telemetryService.GetSuccessRateAsync();
        return Results.Ok(new { title = "Success vs. Failure Rate", data });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving success rate");
        return Results.Problem($"Error retrieving success rate: {ex.Message}");
    }
})
.WithName("GetSuccessRate");

// 🧠 Query 5: Response efficiency (tokens proxy)
monitoringGroup.MapGet("/response-efficiency", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger) =>
{
    try
    {
        var data = await telemetryService.GetResponseEfficiencyAsync();
        return Results.Ok(new { title = "Response Efficiency", data });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving response efficiency");
        return Results.Problem($"Error retrieving response efficiency: {ex.Message}");
    }
})
.WithName("GetResponseEfficiency");

// 🤖 Query 6: Model utilization per agent
monitoringGroup.MapGet("/model-utilization", async (
    TelemetryQueryService telemetryService,
    ILogger<Program> logger) =>
{
    try
    {
        var data = await telemetryService.GetModelUtilizationAsync();
        return Results.Ok(new { title = "Model Utilization per Agent", data });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving model utilization");
        return Results.Problem($"Error retrieving model utilization: {ex.Message}");
    }
})
.WithName("GetModelUtilization");

// GET endpoint to test Application Insights connection
monitoringGroup.MapGet("/test-connection", async (
    IConfiguration configuration,
    ILogger<Program> logger) =>
{
    try
    {
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        var hasConnectionString = !string.IsNullOrEmpty(connectionString);
        
        // Test if we can create a tracer provider
        var testProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = connectionString;
            })
            .Build();
        
        var result = new
        {
            HasConnectionString = hasConnectionString,
            ConnectionStringLength = connectionString?.Length ?? 0,
            ConnectionStringPreview = hasConnectionString ? 
                connectionString!.Substring(0, Math.Min(50, connectionString.Length)) + "..." : "Not configured",
            TracerProviderCreated = testProvider != null,
            Timestamp = DateTime.UtcNow
        };
        
        logger.LogInformation("Application Insights connection test: {Result}", result);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error testing Application Insights connection");
        return Results.Problem($"Error testing connection: {ex.Message}");
    }
})
.WithName("TestApplicationInsightsConnection");

// GET endpoint to test if traces are being sent to Application Insights
monitoringGroup.MapGet("/test-traces", async (
    ILogger<Program> logger) =>
{
    try
    {
        // Create a test activity to verify tracing is working
        using var activitySource = new ActivitySource("AgentCouncil.Agents");
        using var activity = activitySource.StartActivity("TestTrace");
        
        activity?.SetTag("test.type", "ApplicationInsightsVerification");
        activity?.SetTag("test.timestamp", DateTime.UtcNow.ToString("O"));
        activity?.AddEvent(new ActivityEvent("test.event", tags: new ActivityTagsCollection 
        { 
            ["message"] = "This is a test trace to verify Application Insights integration" 
        }));
        
        var result = new
        {
            TestTraceCreated = activity != null,
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            Message = "Test trace created and should be sent to Application Insights",
            Timestamp = DateTime.UtcNow
        };
        
        logger.LogInformation("Test trace created: {TraceId}", activity?.TraceId);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating test trace");
        return Results.Problem($"Error creating test trace: {ex.Message}");
    }
})
.WithName("TestTraces");

// ========================================
// AG UI Protocol Endpoints for CopilotKit
// ========================================
var aguiGroup = app.MapGroup("/api/agui");

// Main chat endpoint - CopilotKit sends messages here
aguiGroup.MapPost("/chat", async (
    AgUiRequest request,
    AgUiService aguiService,
    ILogger<Program> logger) =>
{
    try
    {
        var response = await aguiService.ProcessRequestAsync(request);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "AG UI chat error");
        return Results.Problem(ex.Message);
    }
})
.WithName("AgUiChat")
.WithOpenApi();

// Agent capabilities - tells CopilotKit what agents are available
aguiGroup.MapGet("/capabilities", (
    AgUiService aguiService,
    string? agent = null) =>
{
    var capabilities = aguiService.GetCapabilities(agent ?? "");
    return Results.Ok(capabilities);
})
.WithName("AgUiCapabilities")
.WithOpenApi();

// List all agents - for UI discovery
aguiGroup.MapGet("/agents", (AgUiService aguiService) =>
{
    var capabilities = aguiService.GetCapabilities("");
    return Results.Ok(new { agents = capabilities.Agents.Keys.ToList() });
})
.WithName("AgUiListAgents")
.WithOpenApi();

app.Run();

// Chat request/response records
record ChatRequest(string Message);
