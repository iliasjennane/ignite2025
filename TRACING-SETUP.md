# Azure AI Foundry Tracing Setup

This document outlines the comprehensive tracing setup for the Agent Council application, following Azure AI Foundry semantic conventions for multi-agent observability.

## Overview

The application is fully instrumented with OpenTelemetry tracing following the [Azure AI Foundry tracing guidelines](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/trace-agents-sdk).

## What's Traced

### 1. Main Execution Flow (`execute_task`)
- **Span**: `execute_task`
- **Purpose**: Captures the complete task execution lifecycle
- **Tags**:
  - `ai.task.type`: "chat"
  - `agent.name`: Agent identifier
  - `message.length`: User message length
  - `message.preview`: Preview of user message
  - `thread.id`: Thread identifier
  - `run.id`: Run identifier
  - `response.length`: Assistant response length
  - `tools.used.count`: Number of tools used
  - `connected.agents.count`: Number of connected agents
  - `run.final_status`: Execution status

### 2. Agent Planning (`agent_planning`)
- **Span**: `agent_planning`
- **Purpose**: Logs agent's internal planning steps
- **Tags**:
  - `agent.name`: Agent identifier
  - `plan.type`: "thread_creation"
  - `thread.id`: Thread identifier

### 3. Agent Invocation (`invoke_agent`)
- **Span**: `invoke_agent`
- **Purpose**: Captures agent execution with full context
- **Tags**:
  - `agent.name`: Agent identifier
  - `agent.id`: Azure AI agent ID
  - `agent.model`: Model being used
  - `thread.id`: Thread identifier
  - `run.id`: Run identifier
  - `run.status`: Current run status
  - `run.poll_count`: Polling iteration count

### 4. Agent-to-Agent Interaction (`agent_to_agent_interaction`)
- **Span**: `agent_to_agent_interaction`
- **Purpose**: Traces communication between agents
- **Tags**:
  - `agent.name`: Agent identifier
  - `interaction.type`: "user_message"
  - `message.role`: "user"

### 5. Tool Execution (`execute_tool`)
- **Span**: `execute_tool`
- **Purpose**: Captures each tool invocation
- **Tags**:
  - `tool.name`: Tool identifier
  - `tool.type`: "function_call"
  - `agent.name`: Agent identifier
  - `run.id`: Run identifier
  - `thread.id`: Thread identifier

### 6. Events

#### Evaluation Event
- **Name**: `Evaluation`
- **Purpose**: Structured evaluation of agent performance
- **Tags**:
  - `name`: "task_completion"
  - `label`: "success" | "error"
  - `tools.count`: Number of tools used
  - `agents.count`: Number of connected agents

#### User Message Event
- **Name**: `user.message`
- **Tags**: `content` (user message)

#### Assistant Response Event
- **Name**: `assistant.response`
- **Tags**: `content` (assistant response)

## Configuration

### Application Insights Connection String
Located in `appsettings.json`:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=...;LiveEndpoint=...;ApplicationId=..."
  }
}
```

### OpenTelemetry Setup
The `Program.cs` file configures:
1. **ASP.NET Core Instrumentation**: Traces HTTP requests
2. **HttpClient Instrumentation**: Traces outbound HTTP calls
3. **Azure Monitor Exporter**: Sends traces to Application Insights
4. **Console Exporter**: For development debugging
5. **Custom ActivitySource**: `AgentCouncil.Agents`

## Viewing Traces

### Azure AI Foundry Portal
1. Navigate to your AI Foundry project
2. Go to **Tracing** in the left navigation
3. Filter traces by agent name, time range, etc.
4. Click on a trace to see detailed spans

### Azure Monitor Application Insights
1. Open Application Insights from your resource group
2. Navigate to **Transaction Search**
3. View end-to-end transaction details
4. Analyze trace dependencies

### In-Code Features
The API includes several endpoints for trace analysis:
- `GET /api/monitoring/traces`: Get recent traces
- `GET /api/monitoring/metrics/{agentName}`: Get agent metrics
- `GET /api/monitoring/errors`: Get recent errors
- `GET /api/monitoring/summary`: Dashboard summary

## ActivityListener for SDK Activities

An `ActivityListener` is configured to capture activities from:
- `Microsoft.Extensions.AI`
- `Agents`
- `Azure.AI`
- `OpenAI`

This captures tool calls and agent interactions that are automatically instrumented by the Azure AI SDK.

## Semantic Conventions Compliance

The tracing implementation follows the Azure AI Foundry multi-agent observability semantic conventions:

| Convention | Implementation |
|------------|----------------|
| `execute_task` | ✅ Main execution span |
| `invoke_agent` | ✅ Agent execution span |
| `agent_planning` | ✅ Planning spans |
| `agent_to_agent_interaction` | ✅ Inter-agent communication |
| `execute_tool` | ✅ Tool invocation spans |
| `Evaluation` events | ✅ Structured evaluation data |
| Tool call arguments/results | ✅ Captured via ActivityListener |

## Best Practices

1. **Consistent Attributes**: All spans use consistent attribute naming
2. **Error Tracking**: Errors are tagged with `error.type` and status is set to `ActivityStatusCode.Error`
3. **Performance Metrics**: Poll counts, durations, and counts are tracked
4. **Content Privacy**: Message previews are limited to 100 characters
5. **Correlation**: All spans include `thread.id` and `run.id` for correlation

## Testing Trace Generation

To verify traces are being generated:
1. Make a request to `/api/agents/{agentName}/chat`
2. Check `/api/monitoring/test-traces` to create a test trace
3. View logs in Application Insights or console
4. Confirm spans appear in Azure AI Foundry portal

## Next Steps

To fully leverage tracing in Azure AI Foundry:
1. Ensure Application Insights is connected to your AI Foundry project
2. Enable content recording if needed (privacy-sensitive)
3. Set up alerts based on trace data
4. Create dashboards for key metrics
5. Use trace data for debugging agent behavior

## References

- [Azure AI Foundry Tracing Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/trace-agents-sdk)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
- [Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable)

