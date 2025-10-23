using System.Net.Http.Json;
using AgentCouncil.BlazorWasm.Models;

namespace AgentCouncil.BlazorWasm.Services;

public class ApiAgentsClient : IAgentsClient
{
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;

    public ApiAgentsClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7213";
    }

    public async Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default)
    {
        try
        {
            var chatRequest = new { Message = request.Query };
            var response = await _http.PostAsJsonAsync($"{_apiBaseUrl}/api/agents/{request.AgentName}/chat", chatRequest, ct);
            response.EnsureSuccessStatusCode();
            
            var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
            
            if (chatResponse != null)
            {
                return new AgentResponse(
                    request.AgentName,
                    chatResponse.Reply ?? "No response from agent"
                );
            }
            
            return new AgentResponse(request.AgentName, "Empty response from API");
        }
        catch (Exception ex)
        {
            return new AgentResponse(request.AgentName, $"Error: {ex.Message}");
        }
    }

    private record ChatResponse(string Reply);
}

