using Azure.Core;
using Azure.Identity;
using System.Text;
using System.Text.Json;

namespace AgentCouncil.API.Services;

public class TelemetryQueryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryQueryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _applicationId;
    private readonly DefaultAzureCredential _credential;

    public TelemetryQueryService(IConfiguration configuration, ILogger<TelemetryQueryService> logger, HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
        
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Application Insights connection string not configured");

        _applicationId = ExtractApplicationId(connectionString) 
            ?? throw new InvalidOperationException("Could not extract ApplicationId from connection string");

        _logger.LogInformation("Using Application Insights ApplicationId: {ApplicationId}", _applicationId);
        _credential = new DefaultAzureCredential();
    }

    private static string? ExtractApplicationId(string connectionString)
    {
        var parts = connectionString.Split(';');
        var appIdPart = parts.FirstOrDefault(p => p.StartsWith("ApplicationId="));
        return appIdPart?.Split('=')[1];
    }

    // Helper method to execute KQL queries
    private async Task<JsonElement> ExecuteQueryAsync(string query, string timespan = "P7D")
    {
        var requestBody = new { query = query, timespan = timespan };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://api.applicationinsights.io/.default" }));
        
        var request = new HttpRequestMessage(HttpMethod.Post, 
            $"https://api.applicationinsights.io/v1/apps/{_applicationId}/query")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {token.Token}");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(responseJson);
    }

    // 🧭 Query 1: Agent and Model Usage
    public async Task<IEnumerable<ModelUtilization>> GetAgentModelUsageAsync()
    {
        try
        {
            var query = @"dependencies
| where timestamp > ago(7d)
| extend 
    agentName = trim("" "", tostring(customDimensions[""agent.name""])),
    modelName = trim("" "", tostring(customDimensions[""agent.model""]))
| where isnotempty(agentName) and agentName != ""unknown_agent""
| where isnotempty(modelName)
| summarize Calls = count() by agentName, modelName
| sort by Calls desc";

            var result = await ExecuteQueryAsync(query);
            var data = new List<ModelUtilization>();

            if (result.TryGetProperty("tables", out var tables) && tables.GetArrayLength() > 0)
            {
                var rows = tables[0].GetProperty("rows");
                foreach (var row in rows.EnumerateArray())
                {
                    data.Add(new ModelUtilization
                    {
                        AgentName = row[0].GetString() ?? "",
                        ModelName = row[1].GetString() ?? "",
                        Calls = row[2].GetInt32()
                    });
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent and model usage");
            return new List<ModelUtilization>();
        }
    }

    // Legacy method for backward compatibility
    public async Task<Dictionary<string, int>> GetAgentUsageAsync()
    {
        try
        {
            var agentModelData = await GetAgentModelUsageAsync();
            var data = new Dictionary<string, int>();

            foreach (var item in agentModelData)
            {
                var key = $"{item.AgentName}-{item.ModelName}";
                data[key] = item.Calls;
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent usage");
            return new Dictionary<string, int>();
        }
    }

    // ⚙️ Query 2: Tool usage by agent
    public async Task<Dictionary<string, object>> GetToolUsageAsync()
    {
        try
        {
            var query = @"dependencies
| where timestamp > ago(7d)
| extend agentName = tostring(customDimensions[""agent.name""]),
         toolUsage = toint(customDimensions[""tool.usage_count""])
| summarize AvgToolUsage = avg(toolUsage), MaxToolUsage = max(toolUsage) by agentName
| sort by AvgToolUsage desc";

            var result = await ExecuteQueryAsync(query);
            var data = new Dictionary<string, object>();

            if (result.TryGetProperty("tables", out var tables) && tables.GetArrayLength() > 0)
            {
                var rows = tables[0].GetProperty("rows");
                foreach (var row in rows.EnumerateArray())
                {
                    data[row[0].GetString() ?? ""] = new { Avg = row[1].GetDouble(), Max = row[2].GetDouble() };
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tool usage");
            return new Dictionary<string, object>();
        }
    }

    // 🧮 Query 2: Agent Complexity Index
    public async Task<IEnumerable<AgentComplexity>> GetAgentComplexityAsync()
    {
        try
        {
            var query = @"dependencies
| where timestamp > ago(7d)
| extend
    agentName = trim("" "", tostring(customDimensions[""agent.name""])),
    toolUsage = todouble(customDimensions[""tool.usage_count""]),
    messageLength = todouble(customDimensions[""message.length""])
| where isnotempty(agentName) and agentName != ""unknown_agent""
| summarize
    TotalCalls = count(),
    AvgToolUsage = avg(coalesce(toolUsage, 0.0)),
    AvgMessageLength = avg(coalesce(messageLength, 0.0)),
    MaxMessageLength = max(coalesce(messageLength, 0.0))
    by agentName
| extend 
    EstimatedTokens = round(AvgMessageLength / 4.0, 1),
    ComplexityRaw = coalesce(AvgToolUsage, 0.0) + (AvgMessageLength / 200.0)
// --- Normalize across all agents ---
| summarize
    MaxComplexity = max(ComplexityRaw)
| extend dummy = 1
| join kind=inner (
    dependencies
    | where timestamp > ago(7d)
    | extend
        agentName = trim("" "", tostring(customDimensions[""agent.name""])),
        toolUsage = todouble(customDimensions[""tool.usage_count""]),
        messageLength = todouble(customDimensions[""message.length""])
    | where isnotempty(agentName) and agentName != ""unknown_agent""
    | summarize
        TotalCalls = count(),
        AvgToolUsage = avg(coalesce(toolUsage, 0.0)),
        AvgMessageLength = avg(coalesce(messageLength, 0.0)),
        MaxMessageLength = max(coalesce(messageLength, 0.0)),
        ComplexityRaw = coalesce(avg(coalesce(toolUsage, 0.0)), 0.0) + (avg(coalesce(messageLength, 0.0)) / 200.0)
        by agentName
    | extend dummy = 1
) on dummy
| extend
    ComplexityIndex = round(100 * (ComplexityRaw / MaxComplexity), 1),
    EstimatedTokens = round(AvgMessageLength / 4.0, 1)
| project agentName, TotalCalls, AvgToolUsage, AvgMessageLength, EstimatedTokens, MaxMessageLength, ComplexityIndex
| sort by ComplexityIndex desc";

            var result = await ExecuteQueryAsync(query);
            var data = new List<AgentComplexity>();

            if (result.TryGetProperty("tables", out var tables) && tables.GetArrayLength() > 0)
            {
                var rows = tables[0].GetProperty("rows");
                foreach (var row in rows.EnumerateArray())
                {
                    data.Add(new AgentComplexity
                    {
                        AgentName = row[0].GetString() ?? "",
                        TotalCalls = row[1].GetInt32(),
                        AvgToolUsage = row[2].GetDouble(),
                        AvgMessageLength = row[3].GetDouble(),
                        EstimatedTokens = row[4].GetDouble(),
                        MaxMessageLength = row[5].GetDouble(),
                        ComplexityIndex = row[6].GetDouble()
                    });
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent complexity");
            return new List<AgentComplexity>();
        }
    }

    // 🚦 Query 4: Success vs. failure rate
    public async Task<Dictionary<string, object>> GetSuccessRateAsync()
    {
        try
        {
            var query = @"dependencies
| where timestamp > ago(7d)
| extend agentName = tostring(customDimensions[""agent.name""]),
         status = tostring(customDimensions[""run.final_status""])
| summarize Total = count(), Failures = countif(status != ""completed"") by agentName
| extend FailureRate = round(100.0 * Failures / Total, 2)
| sort by FailureRate desc";

            var result = await ExecuteQueryAsync(query);
            var data = new Dictionary<string, object>();

            if (result.TryGetProperty("tables", out var tables) && tables.GetArrayLength() > 0)
            {
                var rows = tables[0].GetProperty("rows");
                foreach (var row in rows.EnumerateArray())
                {
                    data[row[0].GetString() ?? ""] = new 
                    { 
                        Total = row[1].GetInt32(),
                        Failures = row[2].GetInt32(),
                        FailureRate = row[3].GetDouble()
                    };
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting success rate");
            return new Dictionary<string, object>();
        }
    }

    // 🧠 Query 5: Response efficiency (tokens proxy)
    public async Task<Dictionary<string, object>> GetResponseEfficiencyAsync()
    {
        try
        {
            var query = @"dependencies
| where timestamp > ago(7d)
| extend agentName = tostring(customDimensions[""agent.name""]),
         responseLength = toint(customDimensions[""response.length""])
| summarize AvgResponseLength = avg(responseLength), MaxResponseLength = max(responseLength) by agentName
| sort by AvgResponseLength desc";

            var result = await ExecuteQueryAsync(query);
            var data = new Dictionary<string, object>();

            if (result.TryGetProperty("tables", out var tables) && tables.GetArrayLength() > 0)
            {
                var rows = tables[0].GetProperty("rows");
                foreach (var row in rows.EnumerateArray())
                {
                    data[row[0].GetString() ?? ""] = new { Avg = row[1].GetDouble(), Max = row[2].GetDouble() };
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response efficiency");
            return new Dictionary<string, object>();
        }
    }

    // 🤖 Query 6: Model utilization per agent
    public async Task<IEnumerable<ModelUtilization>> GetModelUtilizationAsync()
    {
        try
        {
            var query = @"dependencies
| where timestamp > ago(7d)
| extend agentName = tostring(customDimensions[""agent.name""]),
         modelName = tostring(customDimensions[""agent.model""])
| summarize Calls = count() by agentName, modelName
| sort by Calls desc";

            var result = await ExecuteQueryAsync(query);
            var data = new List<ModelUtilization>();

            if (result.TryGetProperty("tables", out var tables) && tables.GetArrayLength() > 0)
            {
                var rows = tables[0].GetProperty("rows");
                foreach (var row in rows.EnumerateArray())
                {
                    data.Add(new ModelUtilization
                    {
                        AgentName = row[0].GetString() ?? "",
                        ModelName = row[1].GetString() ?? "",
                        Calls = row[2].GetInt32()
                    });
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model utilization");
            return new List<ModelUtilization>();
        }
    }

    public async Task<IEnumerable<AgentTrace>> GetRecentTracesAsync(int count = 50, string? agentName = null)
    {
        // Return mock data for now
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
                Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440)),
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

    public async Task<AgentMetrics> GetAgentMetricsAsync(string agentName, int hours = 24)
    {
        // Mock data
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

    public async Task<IEnumerable<AgentError>> GetRecentErrorsAsync(int count = 20)
    {
        // Mock data
        return Enumerable.Empty<AgentError>();
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            var agentUsage = await GetAgentUsageAsync();
            
            var totalCalls = agentUsage.Values.Sum();
            var uniqueAgents = agentUsage.Count;

            return new DashboardSummary
            {
                TimeRange = "7d",
                TotalCalls = totalCalls,
                UniqueAgents = uniqueAgents,
                AverageDuration = TimeSpan.FromSeconds(0),
                ErrorCount = 0,
                SuccessRate = 100,
                TotalMessageLength = 0,
                TotalResponseLength = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Application Insights");
            return new DashboardSummary { TimeRange = "7d" };
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

public class ModelUtilization
{
    public string AgentName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public int Calls { get; set; }
}

public class AgentComplexity
{
    public string AgentName { get; set; } = "";
    public int TotalCalls { get; set; }
    public double AvgToolUsage { get; set; }
    public double AvgMessageLength { get; set; }
    public double EstimatedTokens { get; set; }
    public double MaxMessageLength { get; set; }
    public double ComplexityIndex { get; set; }
}
