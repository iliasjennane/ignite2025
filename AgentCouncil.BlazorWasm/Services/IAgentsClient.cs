using AgentCouncil.BlazorWasm.Models;

namespace AgentCouncil.BlazorWasm.Services;

public interface IAgentsClient
{
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default);
}