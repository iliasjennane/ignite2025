using System.Text.Json;
using AgentCouncil.BlazorWasm.Models;

namespace AgentCouncil.BlazorWasm.Services;

public class MonitoringClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MonitoringClient> _logger;
    private readonly string _apiBaseUrl;

    public MonitoringClient(HttpClient httpClient, ILogger<MonitoringClient> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7213";
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/monitoring/summary");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var summary = JsonSerializer.Deserialize<DashboardSummary>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return summary ?? new DashboardSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard summary");
            throw;
        }
    }

    public async Task<IEnumerable<AgentTrace>> GetRecentTracesAsync(int count = 50, string? agentName = null)
    {
        try
        {
            var url = $"{_apiBaseUrl}/api/monitoring/traces?count={count}";
            if (!string.IsNullOrEmpty(agentName))
            {
                url += $"&agentName={Uri.EscapeDataString(agentName)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TracesResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Traces ?? Enumerable.Empty<AgentTrace>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent traces");
            throw;
        }
    }

    public async Task<AgentMetrics> GetAgentMetricsAsync(string agentName, int hours = 24)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/monitoring/metrics/{Uri.EscapeDataString(agentName)}?hours={hours}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var metrics = JsonSerializer.Deserialize<AgentMetrics>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return metrics ?? new AgentMetrics { AgentName = agentName, TimeRange = $"{hours}h" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics for agent {AgentName}", agentName);
            throw;
        }
    }

    public async Task<IEnumerable<AgentError>> GetRecentErrorsAsync(int count = 20)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/monitoring/errors?count={count}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ErrorsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Errors ?? Enumerable.Empty<AgentError>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent errors");
            throw;
        }
    }

    public async Task<IEnumerable<ModelUtilization>> GetAgentModelUsageAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/monitoring/agent-usage");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgentModelUsageResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Data ?? Enumerable.Empty<ModelUtilization>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent and model usage");
            throw;
        }
    }

    // Legacy method for backward compatibility
    public async Task<Dictionary<string, int>> GetAgentUsageAsync()
    {
        var data = await GetAgentModelUsageAsync();
        var dict = new Dictionary<string, int>();
        
        foreach (var item in data)
        {
            var key = $"{item.AgentName}-{item.ModelName}";
            dict[key] = item.Calls;
        }
        
        return dict;
    }

    public async Task<IEnumerable<AgentComplexity>> GetAgentComplexityAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/monitoring/tool-usage");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgentComplexityResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Data ?? Enumerable.Empty<AgentComplexity>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent complexity");
            throw;
        }
    }

    // Legacy method for backward compatibility
    public async Task<Dictionary<string, object>> GetToolUsageAsync()
    {
        var data = await GetAgentComplexityAsync();
        var dict = new Dictionary<string, object>();
        
        foreach (var item in data)
        {
            dict[item.AgentName] = new { 
                TotalCalls = item.TotalCalls,
                AvgToolUsage = item.AvgToolUsage,
                AvgMessageLength = item.AvgMessageLength,
                EstimatedTokens = item.EstimatedTokens,
                MaxMessageLength = item.MaxMessageLength,
                ComplexityIndex = item.ComplexityIndex
            };
        }
        
        return dict;
    }

    public async Task<IEnumerable<ModelUtilization>> GetModelUtilizationAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/monitoring/model-utilization");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ModelUtilizationResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Data ?? Enumerable.Empty<ModelUtilization>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model utilization");
            throw;
        }
    }
}

// Response model for agent usage
public class AgentUsageResponse
{
    public string? Title { get; set; }
    public Dictionary<string, int> Data { get; set; } = new();
}

public class AgentModelUsageResponse
{
    public string? Title { get; set; }
    public IEnumerable<ModelUtilization> Data { get; set; } = Enumerable.Empty<ModelUtilization>();
}

public class ToolUsageResponse
{
    public string? Title { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class AgentComplexityResponse
{
    public string? Title { get; set; }
    public IEnumerable<AgentComplexity> Data { get; set; } = Enumerable.Empty<AgentComplexity>();
}

public class ModelUtilizationResponse
{
    public string? Title { get; set; }
    public IEnumerable<ModelUtilization> Data { get; set; } = Enumerable.Empty<ModelUtilization>();
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

// Response models for API deserialization
public class TracesResponse
{
    public IEnumerable<AgentTrace> Traces { get; set; } = Enumerable.Empty<AgentTrace>();
}

public class ErrorsResponse
{
    public IEnumerable<AgentError> Errors { get; set; } = Enumerable.Empty<AgentError>();
}

// Data models (matching the API models)
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
