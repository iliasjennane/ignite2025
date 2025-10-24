using System.Collections.Concurrent;
using System.Diagnostics;
using Azure;
using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;

namespace AgentCouncil.API.Services;

public class FoundryAgentProvider
{
    private static readonly ActivitySource s_activitySource = new("AgentCouncil.Agents");
    
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoundryAgentProvider> _logger;
    private readonly ConcurrentDictionary<string, AIProjectClient> _projectClients = new();
    private readonly ConcurrentDictionary<string, PersistentAgentsClient> _agentClients = new();
    private readonly ConcurrentDictionary<string, PersistentAgent> _agents = new();

    public FoundryAgentProvider(IConfiguration configuration, ILogger<FoundryAgentProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private async Task<(string Endpoint, string AgentId)> GetAgentConfigAsync(string agentName)
    {
        var config = _configuration.GetSection($"FoundryAgents:{agentName}");
        var endpoint = config["Endpoint"] ?? throw new InvalidOperationException($"Endpoint not configured for agent {agentName}");
        var agentId = config["AgentId"] ?? throw new InvalidOperationException($"AgentId not configured for agent {agentName}");

        _logger.LogInformation("Using endpoint {Endpoint} and agentId {AgentId} for agent {AgentName}", endpoint, agentId, agentName);
        
        return (endpoint, agentId);
    }

    private async Task<AIProjectClient> GetProjectClientAsync(string endpoint)
    {
        return _projectClients.GetOrAdd(endpoint, (ep) =>
        {
            var credential = new DefaultAzureCredential();
            return new AIProjectClient(new Uri(ep), credential);
        });
    }

    private async Task<PersistentAgentsClient> GetAgentClientAsync(string endpoint)
    {
        var projectClient = await GetProjectClientAsync(endpoint);
        return _agentClients.GetOrAdd(endpoint, (ep) => projectClient.GetPersistentAgentsClient());
    }

    private async Task<PersistentAgent> GetAgentAsync(string agentName)
    {
        if (_agents.TryGetValue(agentName, out var cachedAgent))
        {
            return cachedAgent;
        }

        var (endpoint, agentId) = await GetAgentConfigAsync(agentName);
        var agentClient = await GetAgentClientAsync(endpoint);
        
        var agent = agentClient.Administration.GetAgent(agentId);
        _agents.TryAdd(agentName, agent);
        
        return agent;
    }

    public async Task<string> SendAsync(string agentName, string userMessage)
    {
        using var activity = s_activitySource.StartActivity($"Agent.{agentName}.Chat");
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("message.length", userMessage.Length);
        
        try
        {
            _logger.LogInformation("Sending message to agent {AgentName}: {Message}", agentName, userMessage);

            var (endpoint, agentId) = await GetAgentConfigAsync(agentName);
            activity?.SetTag("agent.id", agentId);
            activity?.SetTag("agent.endpoint", endpoint);
            _logger.LogInformation("Got config - Endpoint: {Endpoint}, AgentId: {AgentId}", endpoint, agentId);
            
            var agentClient = await GetAgentClientAsync(endpoint);
            _logger.LogInformation("Got agent client for endpoint: {Endpoint}", endpoint);
            
            var agent = await GetAgentAsync(agentName);
            activity?.SetTag("agent.model", agent.Model ?? "unknown");
            _logger.LogInformation("Got agent: {AgentId}, Model: {Model}", agent.Id, agent.Model);

            _logger.LogInformation("Creating thread for agent {AgentName}", agentName);
            
            // Create a new thread
            using var threadActivity = s_activitySource.StartActivity($"Agent.{agentName}.CreateThread");
            var threadResponse = agentClient.Threads.CreateThread();
            var thread = threadResponse.Value;
            threadActivity?.SetTag("thread.id", thread.Id);
            _logger.LogInformation("Created thread {ThreadId} for agent {AgentName}", thread.Id, agentName);

            // Add user message to thread
            using var messageActivity = s_activitySource.StartActivity($"Agent.{agentName}.AddMessage");
            var messageResponse = agentClient.Messages.CreateMessage(
                thread.Id,
                MessageRole.User,
                userMessage);
            messageActivity?.SetTag("message.role", "user");
            messageActivity?.AddEvent(new ActivityEvent("user.message", tags: new ActivityTagsCollection { ["content"] = userMessage }));
            _logger.LogInformation("Added user message to thread {ThreadId}", thread.Id);

            // Create and run the agent
            using var runActivity = s_activitySource.StartActivity($"Agent.{agentName}.Run");
            var runResponse = agentClient.Runs.CreateRun(
                thread.Id,
                agent.Id);
            var run = runResponse.Value;
            runActivity?.SetTag("run.id", run.Id);
            _logger.LogInformation("Created run {RunId} for agent {AgentName}", run.Id, agentName);

            // Poll for completion
            var pollCount = 0;
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                var runStatusResponse = agentClient.Runs.GetRun(thread.Id, run.Id);
                run = runStatusResponse.Value;
                runActivity?.SetTag("run.status", run.Status.ToString());
                runActivity?.SetTag("run.poll_count", ++pollCount);
                _logger.LogInformation("Run {RunId} status: {Status}", run.Id, run.Status);
                
                // Note: Run steps API has compatibility issues, skipping for now
                // TODO: Implement tool usage tracking when API is stable
            }
            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

            if (run.Status != RunStatus.Completed)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Run failed with status: {run.Status}");
                activity?.SetTag("run.final_status", run.Status.ToString());
                activity?.SetTag("run.error", run.LastError?.Message ?? "Unknown error");
                _logger.LogError("Run failed for agent {AgentName} with status: {Status}, Error: {Error}", 
                    agentName, run.Status, run.LastError?.Message);
                return $"Agent {agentName} run failed with status: {run.Status}. Error: {run.LastError?.Message}";
            }

            // Get the assistant's response
            using var responseActivity = s_activitySource.StartActivity($"Agent.{agentName}.GetResponse");
            var messagesResponse = agentClient.Messages.GetMessages(thread.Id, order: ListSortOrder.Ascending);
            var messages = messagesResponse;
            responseActivity?.SetTag("message.count", messages.Count());
            _logger.LogInformation("Retrieved {MessageCount} messages from thread {ThreadId}", messages.Count(), thread.Id);
            
            // Enhanced logging for tool usage and execution details
            var toolUsageCount = 0;
            var functionCallCount = 0;
            var stepCount = 0;
            
            foreach (var msg in messages)
            {
                _logger.LogInformation("Message: Role={Role}, CreatedAt={CreatedAt}, ContentItems={ContentItemCount}", 
                    msg.Role, msg.CreatedAt, msg.ContentItems.Count());
                
                // Check for tool usage in message content
                foreach (var contentItem in msg.ContentItems)
                {
                    if (contentItem is MessageTextContent textContent)
                    {
                        // Look for tool usage patterns in the text
                        var text = textContent.Text;
                        if (text.Contains("tool") || text.Contains("function") || text.Contains("step"))
                        {
                            toolUsageCount++;
                        }
                    }
                }
                
                // Note: Annotations API has compatibility issues, skipping for now
                // TODO: Implement annotation tracking when API is stable
            }
            
            // Log tool usage information
            responseActivity?.SetTag("tool.usage_count", toolUsageCount);
            responseActivity?.SetTag("function.call_count", functionCallCount);
            responseActivity?.SetTag("execution.step_count", stepCount);
            
            var assistantMessage = messages
                .Where(m => m.Role.ToString().ToLowerInvariant() == "assistant")
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            if (assistantMessage != null)
            {
                var responseText = string.Empty;
                foreach (var contentItem in assistantMessage.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                    {
                        responseText += textItem.Text;
                    }
                    else if (contentItem is MessageImageFileContent imageFileItem)
                    {
                        responseText += $"[Image from ID: {imageFileItem.FileId}]";
                    }
                }
                
                activity?.SetTag("response.length", responseText.Length);
                activity?.SetTag("run.final_status", "completed");
                activity?.AddEvent(new ActivityEvent("assistant.response", tags: new ActivityTagsCollection { ["content"] = responseText }));
                _logger.LogInformation("Received response from agent {AgentName}: {ResponseLength} characters", agentName, responseText.Length);
                return responseText;
            }
            else
            {
                activity?.SetTag("run.final_status", "completed_no_response");
                _logger.LogWarning("No assistant message found in completed run for agent {AgentName}", agentName);
                return $"Agent {agentName} completed but no response was generated.";
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            _logger.LogError(ex, "Error sending message to agent {AgentName}", agentName);
            return $"❌ Error processing chat for {agentName}: {ex.Message}";
        }
    }

    public async Task<object> GetAgentInfoAsync(string agentName)
    {
        try
        {
            var (endpoint, agentId) = await GetAgentConfigAsync(agentName);
            var agent = await GetAgentAsync(agentName);
            
            return new { 
                Id = agentId, 
                Model = agent.Model ?? "gpt-4o-mini", 
                Description = $"Agent {agentName} - {agent.Description ?? "Azure AI Foundry Agent"}" 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent info for {AgentName}", agentName);
            return new
            {
                Id = "unknown",
                Name = agentName,
                Model = "unknown",
                Description = $"Agent {agentName} - Error: {ex.Message}"
            };
        }
    }

    public async Task<string?> GetThreadIdAsync(string agentName)
    {
        try
        {
            await GetAgentConfigAsync(agentName); // Ensure config is loaded
            return null; // We create new threads for each conversation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thread ID for agent {AgentName}", agentName);
            return null;
        }
    }
}