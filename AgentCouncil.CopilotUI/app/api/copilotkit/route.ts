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

interface GraphqlContextShape {
  properties?: Record<string, unknown>;
}

type TextMessageShape = {
  role: string;
  content: string;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function extractAgentIdFromContext(context?: GraphqlContextShape): string | undefined {
  if (!context?.properties) {
    return undefined;
  }

  const candidate = context.properties.agentId;
  return typeof candidate === 'string' ? candidate : undefined;
}

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
  const requestRecord = request as unknown as Record<string, unknown>;
    const graphqlContextCandidate = requestRecord.graphqlContext;
    const graphqlContext = isRecord(graphqlContextCandidate)
      ? (graphqlContextCandidate as GraphqlContextShape)
      : undefined;
    const requestContext = requestRecord.context;

    console.log('[AgentCouncil] request.agentSession:', request.agentSession);
    console.log('[AgentCouncil] request.context:', requestContext);
    console.log('[AgentCouncil] graphqlContext?.properties:', graphqlContext?.properties);

    const agentFromProperties = extractAgentIdFromContext(graphqlContext);
    const agent = agentFromProperties || request.agentSession?.agentName || this.defaultAgent;

  console.log('[AgentCouncil] Processing request for agent:', agent, 'with', request.messages.length, 'messages');

    // Map CopilotKit messages to our backend format
    const messages = request.messages
      .filter(msg => msg.isTextMessage())
      .map(msg => {
        const textMsg = msg as unknown as TextMessageShape;
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
  // @ts-expect-error - Next.js fetch typing omits Node HTTPS agents
      agent: apiUrl.startsWith('https') ? insecureHttpsAgent : undefined
    });

    if (!res.ok) {
      const text = await res.text();
      console.error('[AgentCouncil] Backend error:', res.status, text);
      throw new Error(`Backend error ${res.status}: ${text}`);
    }

    const data = await res.json();
    console.log('[AgentCouncil] Backend response:', data.role, data.content?.substring(0, 100));
    console.log('[AgentCouncil] Backend metadata:', JSON.stringify(data.metadata || {}));

    // Stream the response back through CopilotKit's event system
    const messageId = `msg-${Date.now()}`;
    const metadata = data.metadata || {};
    
    // Send complete message as text stream
    // We'll inject metadata via a script tag in the content (workaround)
    // Or use a custom event that the client can listen to
    const contentWithMetadata = data.content || '';
    
    request.eventSource.stream(async (eventStream$) => {
      eventStream$.sendTextMessageStart({ messageId });
      
      // Inject metadata as a hidden script tag in the content
      // The client-side code will extract this
      const metadataScript = `<script type="application/json" data-agent-council-metadata>${JSON.stringify(metadata)}</script>`;
      const fullContent = contentWithMetadata + metadataScript;
      
      eventStream$.sendTextMessageContent({ 
        messageId, 
        content: fullContent
      });
      eventStream$.sendTextMessageEnd({ messageId });
      eventStream$.complete();
    });

    // Return response with agent-specific thread ID to separate conversations per agent
    // Use agent ID in thread ID to ensure each agent has its own conversation history
    const agentThreadId = `thread-${agent}-${request.threadId || Date.now()}`;
    return {
      threadId: agentThreadId,
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
      // @ts-expect-error - Next.js fetch typing omits Node HTTPS agents for dev
      agent: apiUrl.startsWith('https') ? insecureHttpsAgent : undefined
    });

    if (capabilitiesRes.ok) {
      const capabilities = await capabilitiesRes.json();
      const capabilityAgents = extractCapabilitiesAgents(capabilities);
      if (capabilityAgents.length > 0) {
        return capabilityAgents;
      }
    }

    const res = await fetch(`${apiUrl}/api/agui/agents`, {
      // @ts-expect-error - Next.js fetch typing omits Node HTTPS agents for dev
      agent: apiUrl.startsWith('https') ? insecureHttpsAgent : undefined
    });

    if (!res.ok) {
      console.error('[AgentCouncil] Failed to load fallback agent list:', res.status);
      return [];
    }

    const agents = await res.json();
    const agentIds = extractAgentIds(agents);
    return agentIds.map((id: string) => ({ id, name: id, description: '' }));
  } catch (error) {
    console.error('[AgentCouncil] Error fetching agents:', error);
    return [];
  }
}

type AgentDescriptor = { id: string; name: string; description: string };

function extractCapabilitiesAgents(payload: unknown): AgentDescriptor[] {
  if (!isRecord(payload)) {
    return [];
  }

  const agentsNode = payload.agents;
  if (!isRecord(agentsNode)) {
    return [];
  }

  return Object.entries(agentsNode).reduce<AgentDescriptor[]>((acc, [id, value]) => {
    if (typeof id !== 'string') {
      return acc;
    }

    const agentRecord = isRecord(value) ? value : {};
    const name = typeof agentRecord.name === 'string' ? agentRecord.name : id;
    const description = typeof agentRecord.description === 'string' ? agentRecord.description : '';
    acc.push({ id, name, description });
    return acc;
  }, []);
}

function extractAgentIds(payload: unknown): string[] {
  if (!isRecord(payload)) {
    return [];
  }

  const agentsNode = payload.agents;
  if (!Array.isArray(agentsNode)) {
    return [];
  }

  return agentsNode.filter((item): item is string => typeof item === 'string');
}

export async function POST(req: Request) {
  const clone = req.clone();
  let parsed: unknown;
  let bodyText: string;
  try {
    bodyText = await clone.text();
    parsed = bodyText ? JSON.parse(bodyText) : undefined;
  } catch (error) {
    console.warn('[AgentCouncil] Failed to parse request body:', error);
    bodyText = '';
  }

  const parsedRecord = isRecord(parsed) ? parsed : undefined;
  const query = getString(parsedRecord?.query);

  if (query && query.includes('availableAgents')) {
    const agents = await getAvailableAgents();
    return NextResponse.json({ data: { availableAgents: { agents } } });
  }

  // Check if this is a loadAgentState query and try to inject stored messages
  if (query && query.includes('loadAgentState')) {
    const threadId = extractThreadId(parsedRecord);
    const agentId = extractAgentIdFromContext(parsedRecord?.graphqlContext as GraphqlContextShape) || 'chief_analyst';
    
    // Try to read stored messages from the request headers (sent by client)
    // The client will send stored messages in a custom header
    const storedMessagesHeader = req.headers.get('x-stored-messages');
    let storedMessages: unknown[] = [];
    
    if (storedMessagesHeader) {
      try {
        storedMessages = JSON.parse(storedMessagesHeader);
        console.log('[AgentCouncil] Received', storedMessages.length, 'stored messages from client header');
      } catch (e) {
        console.warn('[AgentCouncil] Failed to parse stored messages from header:', e);
      }
    }

    // Use the threadId from the request if it's agent-scoped, otherwise generate one
    // The threadId should come from the frontend's persisted thread IDs
    let agentThreadId: string;
    if (threadId && threadId.startsWith(`thread-${agentId}`)) {
      // Use the provided thread ID (from frontend's localStorage)
      agentThreadId = threadId;
      console.log('[AgentCouncil] loadAgentState: Restoring thread for agent', agentId, 'threadId:', agentThreadId);
    } else {
      // Generate a new thread ID for this agent
      agentThreadId = `thread-${agentId}-${Date.now()}`;
      console.log('[AgentCouncil] loadAgentState: Creating new thread for agent', agentId, 'threadId:', agentThreadId);
    }
    
    // Return stored messages if provided by client
    const messagesToReturn = Array.isArray(storedMessages) && storedMessages.length > 0 
      ? storedMessages 
      : [];
    
    if (messagesToReturn.length > 0) {
      console.log('[AgentCouncil] loadAgentState: Restoring', messagesToReturn.length, 'messages for agent', agentId);
    }
    
    return NextResponse.json({
      data: {
        loadAgentState: {
          threadId: agentThreadId,
          threadExists: messagesToReturn.length > 0,
          state: null,
          messages: messagesToReturn // Return stored messages if provided by client
        }
      }
    });
  }

  return handler.POST(req);
}

function getString(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

function extractThreadId(parsed: Record<string, unknown> | undefined): string | null {
  if (!parsed) {
    return null;
  }

  const variables = isRecord(parsed.variables) ? parsed.variables : undefined;
  const dataNode = isRecord(variables?.data) ? variables.data : undefined;
  const threadId = getString(dataNode?.threadId);
  return threadId ?? null;
}
