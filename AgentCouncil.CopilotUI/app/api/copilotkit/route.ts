import {
  CopilotRuntime,
  CopilotServiceAdapter,
  CopilotRuntimeChatCompletionRequest,
  CopilotRuntimeChatCompletionResponse,
  copilotRuntimeNextJSAppRouterEndpoint
} from '@copilotkit/runtime';
import https from 'https';
import { NextResponse } from 'next/server';

export const runtime = 'nodejs'; // Needed for https agent and streaming

const apiUrl = process.env.NEXT_PUBLIC_API_URL;
if (!apiUrl) {
  console.error('[CopilotKit] Missing NEXT_PUBLIC_API_URL');
}

// Dev https agent (accept self-signed certs)
const insecureHttpsAgent = new https.Agent({ rejectUnauthorized: false });

/**
 * Custom service adapter that forwards to our .NET AG UI protocol backend.
 * Implements CopilotServiceAdapter interface.
 */
class AgentCouncilServiceAdapter implements CopilotServiceAdapter {
  private defaultAgent: string;

  constructor(defaultAgent: string = 'chief_analyst') {
    this.defaultAgent = defaultAgent;
  }

  async process(
    request: CopilotRuntimeChatCompletionRequest
  ): Promise<CopilotRuntimeChatCompletionResponse> {
    if (!apiUrl) {
      throw new Error('API URL not configured');
    }

    // Extract agent from request context (passed from frontend via CopilotKit agent prop)
  const graphqlContext = (request as any)?.graphqlContext;
  console.log('[AgentCouncil] request.agentSession:', request.agentSession);
  console.log('[AgentCouncil] request.context:', (request as any)?.context);
  console.log('[AgentCouncil] graphqlContext?.properties:', graphqlContext?.properties);

  const agentFromProperties = graphqlContext?.properties?.agentId as string | undefined;
  const agent = agentFromProperties || request.agentSession?.agentName || this.defaultAgent;

  console.log('[AgentCouncil] Processing request for agent:', agent, 'with', request.messages.length, 'messages');

    // Map CopilotKit messages to our backend format
    const messages = request.messages
      .filter(msg => msg.isTextMessage())
      .map(msg => {
        const textMsg = msg as any; // Cast to access text message properties
        return {
          role: textMsg.role,
          content: textMsg.content
        };
      });

    // Forward to backend AG UI chat
    const outbound = {
      agent,
      messages,
      stream: false
    };

    const res = await fetch(`${apiUrl}/api/agui/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(outbound),
      // @ts-ignore
      agent: apiUrl.startsWith('https') ? insecureHttpsAgent : undefined
    });

    if (!res.ok) {
      const text = await res.text();
      console.error('[AgentCouncil] Backend error:', res.status, text);
      throw new Error(`Backend error ${res.status}: ${text}`);
    }

    const data = await res.json();
    console.log('[AgentCouncil] Backend response:', data.role, data.content?.substring(0, 100));

    // Stream the response back through CopilotKit's event system
    const messageId = `msg-${Date.now()}`;
    
    // Send complete message as text stream
    request.eventSource.stream(async (eventStream$) => {
      eventStream$.sendTextMessageStart({ messageId });
      eventStream$.sendTextMessageContent({ messageId, content: data.content || '' });
      eventStream$.sendTextMessageEnd({ messageId });
      eventStream$.complete();
    });

    return {
      threadId: request.threadId || `thread-${Date.now()}`,
      runId: request.runId
    };
  }
}

/**
 * Create CopilotKit runtime endpoint with our custom adapter
 */
const serviceAdapter = new AgentCouncilServiceAdapter('chief_analyst');

// Initialize runtime with agent support disabled to use our custom adapter routing
const copilotRuntime = new CopilotRuntime({
  // Don't use built-in agent routing; our adapter handles it
  delegateAgentProcessingToServiceAdapter: true
});

const handler = copilotRuntimeNextJSAppRouterEndpoint({
  runtime: copilotRuntime,
  serviceAdapter,
  endpoint: '/api/copilotkit'
});

export const { GET, OPTIONS } = handler;

async function getAvailableAgents() {
  if (!apiUrl) {
    return [];
  }

  try {
    const capabilitiesRes = await fetch(`${apiUrl}/api/agui/capabilities`, {
      // @ts-ignore Allow self-signed certs in dev
      agent: apiUrl.startsWith('https') ? insecureHttpsAgent : undefined
    });

    if (capabilitiesRes.ok) {
      const capabilities = await capabilitiesRes.json();
      const agentEntries = capabilities?.agents ? Object.entries(capabilities.agents) : [];
      if (agentEntries.length > 0) {
        return agentEntries.map(([id, value]: [string, any]) => ({
          id,
          name: value?.name ?? id,
          description: value?.description ?? ''
        }));
      }
    }

    const res = await fetch(`${apiUrl}/api/agui/agents`, {
      // @ts-ignore Allow self-signed certs in dev
      agent: apiUrl.startsWith('https') ? insecureHttpsAgent : undefined
    });

    if (!res.ok) {
      console.error('[AgentCouncil] Failed to load fallback agent list:', res.status);
      return [];
    }

    const agents = await res.json();
    const agentIds: string[] = Array.isArray(agents?.agents) ? agents.agents : [];
    return agentIds.map((id) => ({ id, name: id, description: '' }));
  } catch (error) {
    console.error('[AgentCouncil] Error fetching agents:', error);
    return [];
  }
}

export async function POST(req: Request) {
  const clone = req.clone();
  let parsed: any;
  try {
    const bodyText = await clone.text();
    parsed = bodyText ? JSON.parse(bodyText) : undefined;
  } catch (error) {
    console.warn('[AgentCouncil] Failed to parse request body:', error);
  }

  const query: string | undefined = parsed?.query;
  if (query && query.includes('availableAgents')) {
    const agents = await getAvailableAgents();
    return NextResponse.json({ data: { availableAgents: { agents } } });
  }

  if (query && query.includes('loadAgentState')) {
    return NextResponse.json({
      data: {
        loadAgentState: {
          threadId: parsed?.variables?.data?.threadId ?? null,
          threadExists: false,
          state: null,
          messages: []
        }
      }
    });
  }

  return handler.POST(req);
}
