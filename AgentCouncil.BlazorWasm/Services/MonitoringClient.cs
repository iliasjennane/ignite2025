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
