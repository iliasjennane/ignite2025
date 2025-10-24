using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using System.Text.Json;

namespace AgentCouncil.API.Services;

public class TelemetryQueryService
{
    private readonly LogsQueryClient _logsClient;
    private readonly ILogger<TelemetryQueryService> _logger;
    private readonly IConfiguration _configuration;

    public TelemetryQueryService(IConfiguration configuration, ILogger<TelemetryQueryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Get Application Insights connection string
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Application Insights connection string not configured");
        }

        // Create LogsQueryClient
        var credential = new DefaultAzureCredential();
        _logsClient = new LogsQueryClient(credential);
    }

    private static string? ExtractWorkspaceId(string connectionString)
    {
        // Extract ApplicationId from connection string
        // Format: InstrumentationKey=xxx;IngestionEndpoint=xxx;LiveEndpoint=xxx;ApplicationId=xxx
        var parts = connectionString.Split(';');
        var appIdPart = parts.FirstOrDefault(p => p.StartsWith("ApplicationId="));
        return appIdPart?.Split('=')[1];
    }



    public async Task<IEnumerable<AgentTrace>> GetRecentTracesAsync(int count = 50, string? agentName = null)
    {
        try
        {
            // For now, return mock data while we work on the Application Insights integration
            // TODO: Implement actual Application Insights querying once the API is working
            var traces = new List<AgentTrace>();
            
            var agents = new[] { "Data_Analyst", "industry_analyst", "market_analyst", "chief_analyst", "ops_analyst", "sales_analyst" };
            var random = new Random();
            
            for (int i = 0; i < Math.Min(count, 10); i++)
            {
                var agent = string.IsNullOrEmpty(agentName) ? agents[random.Next(agents.Length)] : agentName;
                traces.Add(new AgentTrace
                {
                    Id = $"trace-{Guid.NewGuid():N}",
                    Name = $"Agent.{agent}.Chat",
                    Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440)), // Last 24 hours
                    Duration = TimeSpan.FromMilliseconds(random.Next(1000, 10000)),
                    Status = random.Next(10) < 8 ? "Information" : "Error",
                    AgentName = agent,
                    AgentId = $"asst_{Guid.NewGuid():N}",
                    MessageLength = random.Next(50, 500),
                    ResponseLength = random.Next(100, 2000),
                    RunStatus = random.Next(10) < 8 ? "completed" : "failed"
                });
            }

            return traces.OrderByDescending(t => t.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying recent traces");
            return Enumerable.Empty<AgentTrace>();
        }
    }

    public async Task<AgentMetrics> GetAgentMetricsAsync(string agentName, int hours = 24)
    {
        try
        {
            // Mock data for now
            var random = new Random();
            var totalCalls = random.Next(10, 100);
            var errorCount = random.Next(0, totalCalls / 10);
            
            return new AgentMetrics
            {
                AgentName = agentName,
                TimeRange = $"{hours}h",
                TotalCalls = totalCalls,
                AverageDuration = TimeSpan.FromMilliseconds(random.Next(2000, 8000)),
                MaxDuration = TimeSpan.FromMilliseconds(random.Next(8000, 15000)),
                MinDuration = TimeSpan.FromMilliseconds(random.Next(500, 2000)),
                ErrorCount = errorCount,
                SuccessCount = totalCalls - errorCount,
                SuccessRate = totalCalls > 0 ? (double)(totalCalls - errorCount) / totalCalls * 100 : 0,
                TotalMessageLength = random.Next(1000, 10000),
                TotalResponseLength = random.Next(5000, 50000)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying metrics for agent {AgentName}", agentName);
            return new AgentMetrics { AgentName = agentName, TimeRange = $"{hours}h" };
        }
    }

    public async Task<IEnumerable<AgentError>> GetRecentErrorsAsync(int count = 20)
    {
        try
        {
            // Mock data for now
            var errors = new List<AgentError>();
            var agents = new[] { "Data_Analyst", "industry_analyst", "market_analyst", "chief_analyst", "ops_analyst", "sales_analyst" };
            var random = new Random();
            var errorMessages = new[]
            {
                "Agent run failed with timeout",
                "Invalid response format received",
                "Authentication failed",
                "Rate limit exceeded",
                "Model unavailable"
            };

            for (int i = 0; i < Math.Min(count, 5); i++)
            {
                errors.Add(new AgentError
                {
                    Id = $"error-{Guid.NewGuid():N}",
                    Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440)),
                    Message = errorMessages[random.Next(errorMessages.Length)],
                    Exception = "System.Exception: Agent execution failed",
                    AgentName = agents[random.Next(agents.Length)],
                    RunStatus = "failed"
                });
            }

            return errors.OrderByDescending(e => e.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying recent errors");
            return Enumerable.Empty<AgentError>();
        }
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            // For now, return mock data while we work on the Application Insights integration
            // TODO: Implement actual Application Insights querying once the API is working
            var random = new Random();
            var totalCalls = random.Next(50, 500);
            var errorCount = random.Next(0, totalCalls / 20);
            
            return new DashboardSummary
            {
                TimeRange = "24h",
                TotalCalls = totalCalls,
                UniqueAgents = 6, // We have 6 agents
                AverageDuration = TimeSpan.FromMilliseconds(random.Next(3000, 7000)),
                ErrorCount = errorCount,
                SuccessRate = totalCalls > 0 ? (double)(totalCalls - errorCount) / totalCalls * 100 : 0,
                TotalMessageLength = random.Next(10000, 100000),
                TotalResponseLength = random.Next(50000, 500000)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying dashboard summary");
            return new DashboardSummary { TimeRange = "24h" };
        }
    }
}

// Data models
public class AgentTrace
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public string Status { get; set; } = "";
    public string? AgentName { get; set; }
    public string? AgentId { get; set; }
    public int MessageLength { get; set; }
    public int ResponseLength { get; set; }
    public string? RunStatus { get; set; }
}

public class AgentMetrics
{
    public string AgentName { get; set; } = "";
    public string TimeRange { get; set; } = "";
    public int TotalCalls { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public int ErrorCount { get; set; }
    public int SuccessCount { get; set; }
    public double SuccessRate { get; set; }
    public int TotalMessageLength { get; set; }
    public int TotalResponseLength { get; set; }
}

public class AgentError
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
    public string Exception { get; set; } = "";
    public string? AgentName { get; set; }
    public string? RunStatus { get; set; }
}

public class DashboardSummary
{
    public string TimeRange { get; set; } = "";
    public int TotalCalls { get; set; }
    public int UniqueAgents { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public int ErrorCount { get; set; }
    public double SuccessRate { get; set; }
    public int TotalMessageLength { get; set; }
    public int TotalResponseLength { get; set; }
}
