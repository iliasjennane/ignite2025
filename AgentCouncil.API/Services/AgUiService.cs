using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCouncil.API.Services;

/// <summary>
/// Service to implement AG UI protocol for CopilotKit compatibility
/// Based on: https://docs.copilotkit.ai/microsoft-agent-framework
/// 
/// This service acts as a bridge between CopilotKit's AG UI protocol and your existing
/// FoundryAgentProvider. It translates requests/responses without changing your agent logic.
/// </summary>
public class AgUiService
{
    private readonly FoundryAgentProvider _agentProvider;
    private readonly ILogger<AgUiService> _logger;

    public AgUiService(FoundryAgentProvider agentProvider, ILogger<AgUiService> logger)
    {
        _agentProvider = agentProvider;
        _logger = logger;
    }

    /// <summary>
    /// Process an AG UI chat request from CopilotKit
    /// </summary>
    public async Task<AgUiResponse> ProcessRequestAsync(AgUiRequest request)
    {
        _logger.LogInformation("AG UI Request - Agent: {Agent}, Messages: {MessageCount}", 
            request.Agent, request.Messages?.Count ?? 0);

        var agentName = request.Agent ?? "chief_analyst";
        var lastMessage = request.Messages?.LastOrDefault()?.Content ?? "";

        if (string.IsNullOrWhiteSpace(lastMessage))
        {
            return new AgUiResponse
            {
                Role = "assistant",
                Content = "Please provide a message to process.",
                Metadata = new AgUiMetadata { Error = "Empty message" }
            };
        }

        try
        {
            // Call your existing agent infrastructure - no changes needed to agent logic!
            var (responseText, toolsUsed, connectedAgents) = 
                await _agentProvider.SendAsync(agentName, lastMessage);

            // Package response in AG UI format for CopilotKit
            return new AgUiResponse
            {
                Role = "assistant",
                Content = responseText,
                ToolCalls = toolsUsed.Select(tool => new AgUiToolCall
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new AgUiFunctionCall
                    {
                        Name = tool,
                        Arguments = "{}"
                    }
                }).ToList(),
                Metadata = new AgUiMetadata
                {
                    Agent = agentName,
                    ConnectedAgents = connectedAgents,
                    ToolsUsed = toolsUsed,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AG UI request for agent {Agent}", agentName);
            return new AgUiResponse
            {
                Role = "assistant",
                Content = "I encountered an error processing your request. Please try again.",
                Metadata = new AgUiMetadata 
                { 
                    Error = ex.Message,
                    Agent = agentName
                }
            };
        }
    }

    /// <summary>
    /// Get agent capabilities - tells CopilotKit what agents are available
    /// </summary>
    public AgUiCapabilitiesResponse GetCapabilities(string agentName)
    {
        var agents = new Dictionary<string, AgUiAgentInfo>
        {
            ["chief_analyst"] = new()
            {
                Name = "Chief Analyst",
                Description = "Executive analytics orchestrator that synthesizes sales, dealer, and inventory insights",
                Capabilities = new[] { "orchestration", "sales", "dealer", "inventory" }
            },
            ["sales_insights"] = new()
            {
                Name = "Sales Insights",
                Description = "Sales performance analyst covering revenue, campaign lift, and product mix",
                Capabilities = new[] { "revenue", "campaign", "product_mix", "regional_sales" }
            },
            ["dealer_performance"] = new()
            {
                Name = "Dealer Performance",
                Description = "Dealer network analyst focused on rankings, tiers, and regional comparisons",
                Capabilities = new[] { "dealer_rankings", "tier_analysis", "regional_benchmarking", "profitability" }
            },
            ["inventory_ops"] = new()
            {
                Name = "Inventory Ops",
                Description = "Inventory specialist monitoring stockouts, inbound supply, and logistics disruption",
                Capabilities = new[] { "inventory_health", "stockouts", "logistics", "turnover" }
            }
        };

        return new AgUiCapabilitiesResponse
        {
            Agents = agents,
            SupportedFeatures = new[] { "streaming", "tool_calls", "multi_agent" },
            Version = "1.0.0"
        };
    }
}

#region AG UI Protocol Models

/// <summary>
/// Request from CopilotKit following AG UI protocol
/// </summary>
public record AgUiRequest(
    [property: JsonPropertyName("agent")] string? Agent,
    [property: JsonPropertyName("messages")] List<AgUiMessage>? Messages,
    [property: JsonPropertyName("stream")] bool Stream = false
);

public record AgUiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

/// <summary>
/// Response to CopilotKit following AG UI protocol
/// </summary>
public class AgUiResponse
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    
    [JsonPropertyName("content")]
    public required string Content { get; init; }
    
    [JsonPropertyName("tool_calls")]
    public List<AgUiToolCall>? ToolCalls { get; init; }
    
    [JsonPropertyName("metadata")]
    public AgUiMetadata? Metadata { get; init; }
}

public class AgUiToolCall
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("function")]
    public required AgUiFunctionCall Function { get; init; }
}

public class AgUiFunctionCall
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

/// <summary>
/// Metadata attached to AG UI responses - shows tools used and connected agents
/// </summary>
public class AgUiMetadata
{
    [JsonPropertyName("agent")]
    public string? Agent { get; init; }
    
    [JsonPropertyName("connected_agents")]
    public List<string>? ConnectedAgents { get; init; }
    
    [JsonPropertyName("tools_used")]
    public List<string>? ToolsUsed { get; init; }
    
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }
    
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Agent capabilities response - describes available agents
/// </summary>
public class AgUiCapabilitiesResponse
{
    [JsonPropertyName("agents")]
    public required Dictionary<string, AgUiAgentInfo> Agents { get; init; }
    
    [JsonPropertyName("supported_features")]
    public required string[] SupportedFeatures { get; init; }
    
    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

public class AgUiAgentInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("capabilities")]
    public required string[] Capabilities { get; init; }
}

#endregion
