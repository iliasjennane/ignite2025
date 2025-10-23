using AgentCouncil.API.Services;
using System.Text.Json;
using Scalar.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry tracing
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("AgentApi")
    .AddConsoleExporter()
    .Build();

// Add services to the container
builder.Services.AddSingleton<FoundryAgentProvider>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
// builder.Services.AddSwaggerGen(); // Commented out due to compatibility issues

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("https://localhost:7263", "http://localhost:5002", "http://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();

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
        logger.LogInformation("Received chat request for agent {AgentName}: {Message}", agentName, request.Message);
        
        var response = await provider.SendAsync(agentName, request.Message);
        
        var result = Results.Ok(new { reply = response });
        return result;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing chat request for agent {AgentName}", agentName);
        return Results.Problem($"Error processing chat: {ex.Message}");
    }
})
.WithName("ChatWithAgent")
.WithOpenApi();

// GET endpoint to list available agents
agentsGroup.MapGet("/", async (FoundryAgentProvider provider, ILogger<Program> logger) =>
{
    try
    {
        var agents = new[]
        {
            "Data_Analyst",
            "industry_analyst", 
            "market_analyst",
            "chief_analyst",
            "ops_analyst",
            "sales_analyst"
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
.WithName("ListAgents")
.WithOpenApi();

app.Run();

// Chat request/response records
record ChatRequest(string Message);
