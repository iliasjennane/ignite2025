namespace AgentCouncil.BlazorWasm.Models;

public record AgentRequest(string AgentName, string Query, Dictionary<string, object>? Context = null);

public record AgentResponse(
    string AgentName,
    string Summary,
    object? Data = null,
    string? Evidence = null,
    List<string>? ToolsUsed = null,
    List<string>? ConnectedAgents = null);

public record ChartDatum(
    string Metric,
    string Region,
    double Value,
    string Period);

public class AgentEndpointsOptions : Dictionary<string, string> { }

public class ChatMessage
{
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
    public bool HasChart { get; set; } = false;
    public string ChartData { get; set; } = "";
    public string ChartTitle { get; set; } = "";
    public string ChartType { get; set; } = "bar";
}