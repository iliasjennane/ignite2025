using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using AgentCouncil.BlazorWasm.Models;

namespace AgentCouncil.BlazorWasm.Services;

public class DirectAgentsClient(HttpClient http, IOptions<AgentEndpointsOptions> endpoints) : IAgentsClient
{
    private readonly HttpClient _http = http;
    private readonly AgentEndpointsOptions _endpoints = endpoints.Value;

    public async Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default)
    {
        if (!_endpoints.TryGetValue(request.AgentName, out var url))
            throw new InvalidOperationException($"No endpoint configured for agent '{request.AgentName}'");

        using var resp = await _http.PostAsJsonAsync(url, new { query = request.Query, context = request.Context }, ct);
        resp.EnsureSuccessStatusCode();
        // Assume the agent returns a compatible shape; map if needed
        var json = await resp.Content.ReadAsStringAsync(ct);
        // naive passthrough: treat entire response as Summary if not JSON-deserializable to AgentResponse
        try
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<AgentResponse>(json, new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive = true});
            if (obj is not null) return obj;
        }
        catch { /* fallback below */ }
        return new AgentResponse(request.AgentName, json);
    }
}