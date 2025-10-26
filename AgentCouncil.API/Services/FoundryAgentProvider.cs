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

    public async Task<(string Response, List<string> ToolsUsed, List<string> ConnectedAgents)> SendAsync(string agentName, string userMessage)
    {
        using var activity = s_activitySource.StartActivity($"Agent.{agentName}.Chat");
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("message.length", userMessage.Length);
        
        var toolsUsed = new List<string>();
        var connectedAgents = new List<string>();
        
        // Set up ActivityListener to capture tool calls from Microsoft Agent Framework
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.Contains("Microsoft.Extensions.AI") || 
                                       source.Name.Contains("Agents") ||
                                       source.Name.Contains("Azure.AI") ||
                                       source.Name.Contains("OpenAI"),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => {
                _logger.LogInformation("Activity started: {ActivityName} from source {SourceName}", 
                    activity.DisplayName, activity.Source.Name);
                
                // Extract tool names from activity tags
                var toolName = activity.GetTagItem("tool.name") ?? 
                              activity.GetTagItem("function.name") ??
                              activity.GetTagItem("agent.tool.call") ??
                              activity.GetTagItem("tool_call.function.name") ??
                              activity.GetTagItem("ai.tool.name");
                
                if (toolName != null)
                {
                    var toolNameStr = toolName.ToString();
                    if (!string.IsNullOrEmpty(toolNameStr) && !toolsUsed.Contains(toolNameStr))
                    {
                        toolsUsed.Add(toolNameStr);
                        _logger.LogInformation("Captured tool call: {ToolName}", toolNameStr);
                    }
                }
                
                // Also check for agent calls
                var agentCallName = activity.GetTagItem("agent.name") ?? 
                                   activity.GetTagItem("assistant.name") ??
                                   activity.GetTagItem("ai.agent.name");
                
                if (agentCallName != null)
                {
                    var agentNameStr = agentCallName.ToString();
                    if (!string.IsNullOrEmpty(agentNameStr) && !connectedAgents.Contains(agentNameStr))
                    {
                        connectedAgents.Add(agentNameStr);
                        _logger.LogInformation("Captured agent call: {AgentName}", agentNameStr);
                    }
                }
                
                // Log all tags for debugging
                foreach (var tag in activity.Tags)
                {
                    _logger.LogInformation("Activity tag: {Key} = {Value}", tag.Key, tag.Value);
                }
            }
        };
        
        ActivitySource.AddActivityListener(listener);
        
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
                return ($"Agent {agentName} run failed with status: {run.Status}. Error: {run.LastError?.Message}", toolsUsed, connectedAgents);
            }

            // Log the raw run object to understand its structure
            _logger.LogInformation("=== RAW RUN OBJECT ===");
            var runJson = System.Text.Json.JsonSerializer.Serialize(run, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("Run JSON: {RunJson}", runJson);
            _logger.LogInformation("=== END RAW RUN OBJECT ===");

            // Capture tool usage from run steps
            try
            {
                var runSteps = agentClient.Runs.GetRunSteps(thread.Id, run.Id);
                var runStepCount = 0;
                
                foreach (var step in runSteps)
                {
                    runStepCount++;
                    _logger.LogInformation("Run step {StepNumber}: Type={Type}, Status={Status}, Id={StepId}", 
                        runStepCount, step.Type, step.Status, step.Id);
                    
                    // Log the actual step properties
                    _logger.LogInformation("Step properties: {Properties}", string.Join(", ", step.GetType().GetProperties().Select(p => p.Name)));
                    
                    // Log StepDetails properties
                    if (step.StepDetails != null)
                    {
                        _logger.LogInformation("StepDetails type: {DetailsType}", step.StepDetails.GetType().Name);
                        _logger.LogInformation("StepDetails properties: {DetailsProperties}", string.Join(", ", step.StepDetails.GetType().GetProperties().Select(p => p.Name)));
                    }
                    
                    // Check for tool calls in the step
                    if (step.Type == "tool_calls")
                    {
                        _logger.LogInformation("Found tool_calls step: {StepId}", step.Id);
                        
                        // Access StepDetails properly based on SDK structure
                        var stepDetails = step.StepDetails;
                        if (stepDetails != null)
                        {
                            _logger.LogInformation("Step details type: {DetailsType}", stepDetails.GetType().Name);
                            
                            // Try multiple approaches to access tool calls
                            try
                            {
                                // Approach 1: Try to cast to ToolCallsStepDetails
                                var stepDetailsType = stepDetails.GetType();
                                _logger.LogInformation("StepDetails properties: {Properties}", string.Join(", ", stepDetailsType.GetProperties().Select(p => p.Name)));
                                
                                // Try to get tool calls using different property names
                                var toolCallsProperty = stepDetailsType.GetProperty("ToolCalls") ?? 
                                                       stepDetailsType.GetProperty("ToolCall") ??
                                                       stepDetailsType.GetProperty("Calls") ??
                                                       stepDetailsType.GetProperty("ToolCallsList");
                                
                                if (toolCallsProperty != null)
                                {
                                    var toolCalls = toolCallsProperty.GetValue(stepDetails);
                                    if (toolCalls != null)
                                    {
                                        _logger.LogInformation("Found tool calls property: {PropertyName}, Type: {Type}", toolCallsProperty.Name, toolCalls.GetType().Name);
                                        
                                        // If it's a collection, iterate through it
                                        if (toolCalls is System.Collections.IEnumerable enumerable)
                                        {
                                            foreach (var toolCall in enumerable)
                                            {
                                                if (toolCall != null)
                                                {
                                                    _logger.LogInformation("Tool call type: {Type}", toolCall.GetType().Name);
                                                    var toolCallType = toolCall.GetType();
                                                    
                                                    // Try to get the tool name from various properties
                                                    var toolName = GetToolNameFromObject(toolCall);
                                                    if (!string.IsNullOrEmpty(toolName) && !toolsUsed.Contains(toolName))
                                                    {
                                                        toolsUsed.Add(toolName);
                                                        _logger.LogInformation("Extracted tool name from run step: {ToolName}", toolName);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Single tool call
                                            var toolName = GetToolNameFromObject(toolCalls);
                                            if (!string.IsNullOrEmpty(toolName) && !toolsUsed.Contains(toolName))
                                            {
                                                toolsUsed.Add(toolName);
                                                _logger.LogInformation("Extracted tool name from run step: {ToolName}", toolName);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback: Log the step details structure for debugging
                                    var detailsJson = System.Text.Json.JsonSerializer.Serialize(stepDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                                    _logger.LogInformation("Step details JSON (no ToolCalls property found): {DetailsJson}", detailsJson);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error processing step details for step {StepId}", step.Id);
                            }
                        }
                    }
                }
                
                _logger.LogInformation("Processed {StepCount} run steps for run {RunId}", runStepCount, run.Id);
                
                // Log what we actually got from the SDK
                _logger.LogInformation("=== SDK DATA ANALYSIS ===");
                _logger.LogInformation("Run ID: {RunId}", run.Id);
                _logger.LogInformation("Run Status: {Status}", run.Status);
                _logger.LogInformation("Run Model: {Model}", run.Model);
                _logger.LogInformation("Run Instructions: {Instructions}", run.Instructions);
                _logger.LogInformation("Run Tools Count: {ToolsCount}", run.Tools?.Count ?? 0);
                _logger.LogInformation("Run Usage: {Usage}", run.Usage != null ? $"Tokens: {run.Usage.TotalTokens}" : "No usage data");
                
                // Log the actual properties available on the ThreadRun object
                _logger.LogInformation("ThreadRun properties: {Properties}", string.Join(", ", run.GetType().GetProperties().Select(p => p.Name)));
                
                activity?.SetTag("tools.count", toolsUsed.Count);
                _logger.LogInformation("Captured {ToolCount} tool calls from run {RunId}", toolsUsed.Count, run.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve run steps for run {RunId}, tool tracking unavailable", run.Id);
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
                return (responseText, toolsUsed, connectedAgents);
            }
            else
            {
                activity?.SetTag("run.final_status", "completed_no_response");
                _logger.LogWarning("No assistant message found in completed run for agent {AgentName}", agentName);
                return ($"Agent {agentName} completed but no response was generated.", toolsUsed, connectedAgents);
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            _logger.LogError(ex, "Error sending message to agent {AgentName}", agentName);
            return ($"❌ Error processing chat for {agentName}: {ex.Message}", toolsUsed, connectedAgents);
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
    
    private string? GetToolNameFromObject(object toolCall)
    {
        try
        {
            var toolCallType = toolCall.GetType();
            _logger.LogInformation("Tool call object type: {Type}", toolCallType.Name);
            _logger.LogInformation("Tool call properties: {Properties}", string.Join(", ", toolCallType.GetProperties().Select(p => p.Name)));
            
            // Try different property names to get the tool name
            var nameProperty = toolCallType.GetProperty("Name") ?? 
                              toolCallType.GetProperty("FunctionName") ??
                              toolCallType.GetProperty("ToolName") ??
                              toolCallType.GetProperty("Id");
            
            if (nameProperty != null)
            {
                var name = nameProperty.GetValue(toolCall)?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            
            // Try to get from Function property
            var functionProperty = toolCallType.GetProperty("Function");
            if (functionProperty != null)
            {
                var function = functionProperty.GetValue(toolCall);
                if (function != null)
                {
                    var functionType = function.GetType();
                    var functionNameProperty = functionType.GetProperty("Name") ?? 
                                             functionType.GetProperty("FunctionName");
                    
                    if (functionNameProperty != null)
                    {
                        var functionName = functionNameProperty.GetValue(function)?.ToString();
                        if (!string.IsNullOrEmpty(functionName))
                        {
                            return functionName;
                        }
                    }
                }
            }
            
            // Try to get from Tool property
            var toolProperty = toolCallType.GetProperty("Tool");
            if (toolProperty != null)
            {
                var tool = toolProperty.GetValue(toolCall);
                if (tool != null)
                {
                    var toolType = tool.GetType();
                    var toolNameProperty = toolType.GetProperty("Name") ?? 
                                         toolType.GetProperty("ToolName");
                    
                    if (toolNameProperty != null)
                    {
                        var toolName = toolNameProperty.GetValue(tool)?.ToString();
                        if (!string.IsNullOrEmpty(toolName))
                        {
                            return toolName;
                        }
                    }
                }
            }
            
            // Log the object structure for debugging
            var json = System.Text.Json.JsonSerializer.Serialize(toolCall, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("Tool call object JSON: {Json}", json);
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting tool name from object");
            return null;
        }
    }
}