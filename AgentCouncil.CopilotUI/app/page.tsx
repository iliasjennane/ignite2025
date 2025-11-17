'use client';

import { CopilotKit } from "@copilotkit/react-core";
import { CopilotChat } from "@copilotkit/react-ui";
import "@copilotkit/react-ui/styles.css";
import { useCallback, useEffect, useMemo, useState, useRef } from "react";
import { useCopilotMessagesContext, useCopilotChat, useCopilotAction } from "@copilotkit/react-core";
import { TextMessage, MessageRole } from "@copilotkit/runtime-client-gql";

const AGENTS = [
  {
    id: 'chief_analyst',
    name: 'Chief Analyst',
    icon: '🎯',
    description: 'Synthesizes sales, dealer, and inventory intelligence into executive answers.',
    color: 'bg-blue-600',
    initialMessage: 'How can I synthesize sales, dealer, and inventory insights into a decision-ready brief?',
    placeholder: 'Ask for cross-domain analysis, campaign risks, or prioritized actions…',
    sampleQueries: [
      'Sales trend vs baseline Jun–Sep 2025 vs Mar–May 2025',
      'Did stockouts hurt EV SUV margin in West Aug–Sep 2025?',
      'Which dealer tiers drove EV SUV growth in the campaign months?',
      'How did delays affect campaign revenue in Pacific NW?',
      'Top 5 dealers driving growth Jun–Sep 2025',
      'Impact of logistics delays on dealer ranking West region Aug–Sep 2025'
    ]
  },
  {
    id: 'sales_insights',
    name: 'Sales Insights',
    icon: '💰',
    description: 'Sales performance analyst for revenue, margin, and campaign lift.',
    color: 'bg-green-600',
    initialMessage: 'What sales performance question should we tackle first?',
    placeholder: 'Ask about campaign revenue, margin shifts, or regional trends…',
    sampleQueries: [
      'What was the total revenue for Azure Motors EV SUVs during June–September 2025?',
      'Compare campaign period revenue (June–September 2025) versus baseline period (March–May 2025) for Azure Motors EV SUVs',
      'Which region had the highest revenue for Azure Motors EV SUVs during the campaign period?',
      'What was the average margin percentage for Azure Motors EV SUVs during the campaign compared to the baseline period?',
      'How did revenue in West and Pacific NW regions change during August–September 2025 compared to June–July 2025?',
      'What was the month-by-month revenue trend for Azure Motors EV SUVs from March through September 2025?'
    ]
  },
  {
    id: 'dealer_performance',
    name: 'Dealer Performance',
    icon: '🏪',
    description: 'Dealer network analyst covering rankings, tiers, and regional comparisons.',
    color: 'bg-purple-600',
    initialMessage: 'Ready to dive into dealer rankings or tier performance insights?',
    placeholder: 'Ask for top dealers, regional comparisons, or tier trends…',
    sampleQueries: [
      'Which top 10 dealers had the highest revenue during Q3 2025?',
      'Compare average dealer profit between West and Pacific NW regions during Q3 2025',
      'How did Tier 1 dealers perform compared to Tier 2 and Tier 3 dealers during the campaign period?',
      'Which dealers had the lowest profit in September 2025?',
      'Show me the top 5 dealers\' revenue trend from June through September 2025',
      'Who was the top performing dealer in the Northeast region during the campaign period?',
      'Compare top 10 dealers\' performance in Q3 2025 versus Q2 2025'
    ]
  },
  {
    id: 'inventory_ops',
    name: 'Inventory Ops',
    icon: '�',
    description: 'Inventory specialist monitoring stockouts, inbound supply, and logistics disruption.',
    color: 'bg-orange-600',
    initialMessage: 'Where should we focus the next inventory or logistics check?',
    placeholder: 'Ask about stockouts, inbound recovery, or regional supply risk…',
    sampleQueries: [
      'How did average incoming inventory in West and Pacific NW regions change during the logistics disruption (Aug–Sep 2025) compared to June–July 2025?',
      'Which dealers experienced stockouts in Pacific NW during August–September 2025?',
      'Which region had the highest average logistics delay factor during the disruption months?',
      'What was average on-hand inventory for Azure Motors EV SUVs during the campaign period vs baseline?',
      'Show top 10 dealers with lowest average on_hand during August–September 2025',
      'Has incoming inventory started recovering in West region in September 2025 compared to August 2025?',
      'Which models show rising turn_rate and falling on_hand over the last three months?'
    ]
  }
] as const;

type AgentId = (typeof AGENTS)[number]["id"];
// Pre-baked prompts so each agent can showcase a guided workflow in the demo.
type QuickAction = { id: string; title: string; description: string; prompt: string };
type AssistantMessageLike = {
  toolCalls?: Array<{
    function?: { name?: string | null } | null;
    name?: string | null;
  }>;
  metadata?: unknown;
  state?: unknown;
  rawMessage?: unknown;
  raw?: unknown;
  extensions?: unknown;
};
type ToolCallLike = NonNullable<AssistantMessageLike["toolCalls"]>[number];

const AGENT_ACTIONS: Record<AgentId, QuickAction[]> = {
  chief_analyst: [
    {
      id: "exec-brief",
      title: "Executive Brief",
      description: "Summarize sales, dealer, and inventory signals into a board-ready briefing.",
      prompt: "Produce a concise executive briefing that synthesizes the most recent cross-domain signals across sales, dealer performance, and inventory. Highlight immediate risks, upside opportunities, and recommended actions for leadership.",
    },
    {
      id: "priority-ladder",
      title: "Priority Ladder",
      description: "Rank actions by impact and effort for the current campaign cadence.",
      prompt: "Create a priority ladder of the top 5 cross-functional actions we should take next. Score each by impact and effort and note which specialist agent should own the follow-up.",
    },
  ],
  sales_insights: [
    {
      id: "campaign-checklist",
      title: "Campaign Checklist",
      description: "Generate a follow-up checklist for the current sales campaign focus.",
      prompt: "Draft a prioritized campaign follow-up checklist that covers pipeline health, high-risk regions, and promo optimizations we must run next.",
    },
    {
      id: "margin-guard",
      title: "Margin Guardrails",
      description: "Flag margin erosion hotspots and safeguards to deploy immediately.",
      prompt: "Identify where margin erosion is accelerating and outline guardrails or levers we can deploy immediately to stabilize profitability.",
    },
  ],
  dealer_performance: [
    {
      id: "dealer-rally",
      title: "Dealer Rally Plan",
      description: "Outline a rally plan for underperforming dealer tiers.",
      prompt: "Create a rally plan for underperforming dealer tiers. Include focus regions, enablement themes, and a communication cadence for field teams.",
    },
    {
      id: "tier-health",
      title: "Tier Health Scan",
      description: "Summarize tier health and flag dealerships needing interventions.",
      prompt: "Run a tier health scan and summarize which dealerships need immediate interventions, the reason, and the recommended partner actions.",
    },
  ],
  inventory_ops: [
    {
      id: "stock-risk",
      title: "Stock Risk Watchlist",
      description: "Surface the next critical stockout hotspots and mitigation steps.",
      prompt: "Build a stock risk watchlist that calls out the next critical stockout hotspots, expected financial impact, and mitigation steps we should coordinate.",
    },
    {
      id: "logistics-sprint",
      title: "Logistics Sprint",
      description: "Recommend logistics sprints to rebalance inventory quickly.",
      prompt: "Recommend a logistics sprint plan to rebalance inventory across regions with the highest upside. Include partner teams we must loop in and success metrics.",
    },
  ],
};

export default function Home() {
  const [selectedAgent, setSelectedAgent] = useState<AgentId>(AGENTS[0].id);
  
  // Store current thread ID per agent - persist in localStorage to maintain conversations across page reloads
  // Each agent maintains its own isolated conversation thread
  // Use useState with useEffect to avoid hydration mismatch (Date.now() differs on server vs client)
  const [agentThreadIds, setAgentThreadIds] = useState<Record<AgentId, string>>({} as Record<AgentId, string>);
  
  // Initialize thread IDs on client side only to avoid hydration mismatch
  useEffect(() => {
    if (typeof window === 'undefined') return;
    
    // Load from localStorage or generate new ones
    const stored = localStorage.getItem('agentCouncilThreadIds');
    if (stored) {
      try {
        const parsed = JSON.parse(stored) as Record<AgentId, string>;
        // Ensure all agents have thread IDs
        const initial: Record<AgentId, string> = {} as Record<AgentId, string>;
        AGENTS.forEach(agent => {
          initial[agent.id] = parsed[agent.id] || `thread-${agent.id}-${Date.now()}`;
        });
        setAgentThreadIds(initial);
        return;
      } catch (e) {
        console.warn('Failed to parse stored thread IDs:', e);
      }
    }
    
    // Generate new thread IDs for all agents
    const initial: Record<AgentId, string> = {} as Record<AgentId, string>;
    AGENTS.forEach(agent => {
      initial[agent.id] = `thread-${agent.id}-${Date.now()}`;
    });
    setAgentThreadIds(initial);
  }, []); // Run only once on mount
  
  // Persist thread IDs to localStorage whenever they change
  useEffect(() => {
    if (typeof window !== 'undefined' && Object.keys(agentThreadIds).length > 0) {
      localStorage.setItem('agentCouncilThreadIds', JSON.stringify(agentThreadIds));
    }
  }, [agentThreadIds]);
  
  // Function to start a new conversation with an agent
  const startNewThread = useCallback((agentId: AgentId) => {
    setAgentThreadIds(prev => {
      const updated = {
        ...prev,
        [agentId]: `thread-${agentId}-${Date.now()}`
      };
      // Persist immediately
      if (typeof window !== 'undefined') {
        localStorage.setItem('agentCouncilThreadIds', JSON.stringify(updated));
      }
      return updated;
    });
  }, []);
  
  const [toolsUsed, setToolsUsed] = useState<string[]>([]);
  const [connectedAgents, setConnectedAgents] = useState<string[]>([]);
  const stableToolsUpdater = useCallback((tools: string[]) => setToolsUsed(tools), []);
  const stableConnectedUpdater = useCallback((agents: string[]) => setConnectedAgents(agents), []);
  
  const currentAgent = AGENTS.find(a => a.id === selectedAgent);
  const defaultInitial = "How can I help you analyze your business data today?";
  const defaultPlaceholder = "Ask me anything about sales, dealers, or inventory...";
  const suggestions = currentAgent?.sampleQueries?.map((message, index) => ({
    title: `Example ${index + 1}`,
    message
  })) ?? [];

  return (
    <div className="h-screen flex bg-gray-50">
      {/* Agent Selector Sidebar */}
      <div className="w-72 bg-gradient-to-b from-gray-900 to-gray-800 text-white shadow-2xl overflow-y-auto">
        <div className="p-6">
          <div className="flex items-center gap-3 mb-8">
            <div className="text-4xl">🤖</div>
            <div>
              <h1 className="text-2xl font-bold">Agent Council</h1>
              <p className="text-sm text-gray-400">CopilotKit Edition</p>
            </div>
          </div>
          
          <div className="space-y-3">
            {AGENTS.map((agent) => (
              <button
                key={agent.id}
                onClick={() => {
                  setSelectedAgent(agent.id);
                  setToolsUsed([]);
                  setConnectedAgents([]);
                }}
                className={`w-full text-left p-4 rounded-xl transition-all duration-200 ${
                  selectedAgent === agent.id
                    ? `${agent.color} shadow-lg scale-105`
                    : 'bg-gray-800 hover:bg-gray-700 hover:scale-102'
                }`}
              >
                <div className="flex items-center gap-3 mb-2">
                  <span className="text-2xl">{agent.icon}</span>
                  <span className="font-semibold text-lg">{agent.name}</span>
                </div>
                <div className="text-xs text-gray-300 ml-11">{agent.description}</div>
              </button>
            ))}
          </div>

          {/* Activity Indicators */}
          {(toolsUsed.length > 0 || connectedAgents.length > 0) && (
            <div className="mt-8 p-4 bg-gray-800 rounded-xl">
              <h3 className="text-sm font-semibold mb-3 text-gray-400">Recent Activity</h3>
              
              {toolsUsed.length > 0 && (
                <div className="mb-3">
                  <div className="text-xs text-gray-500 mb-1">Tools Used</div>
                  <div className="flex flex-wrap gap-1">
                    {toolsUsed.slice(0, 5).map((tool, i) => (
                      <span key={i} className="text-xs bg-blue-900 text-blue-200 px-2 py-1 rounded">
                        {tool}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              
              {connectedAgents.length > 0 && (
                <div>
                  <div className="text-xs text-gray-500 mb-1">Connected Agents</div>
                  <div className="flex flex-wrap gap-1">
                    {connectedAgents.map((agent, i) => (
                      <span key={i} className="text-xs bg-purple-900 text-purple-200 px-2 py-1 rounded">
                        {agent}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col">
        {/* Header */}
        <div className="bg-white border-b shadow-sm p-6">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className={`${currentAgent?.color} w-16 h-16 rounded-2xl flex items-center justify-center text-3xl shadow-lg`}>
                {currentAgent?.icon}
              </div>
              <div>
                <h2 className="text-2xl font-bold text-gray-900">{currentAgent?.name}</h2>
                <p className="text-sm text-gray-600">{currentAgent?.description}</p>
              </div>
            </div>
            <button
              onClick={() => startNewThread(selectedAgent)}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
              title="Start a new conversation with this agent"
            >
              New Conversation
            </button>
          </div>
        </div>

        {/* CopilotKit Chat Interface */}
        <div className="flex-1 bg-gray-100">
          {/* Each agent gets its own CopilotKit instance to ensure complete isolation */}
          {/* Using key based on agent ensures separate instances, and CopilotKit should restore from storage via threadId */}
          {/* Only render CopilotKit after thread IDs are initialized to avoid hydration issues */}
          {typeof window !== 'undefined' && agentThreadIds[selectedAgent] && (
            <CopilotKit
              key={selectedAgent} // Key by agent - each agent gets its own instance with its own thread
              runtimeUrl="/api/copilotkit"
              properties={{ agentId: selectedAgent, threadId: agentThreadIds[selectedAgent] }}
              publicApiKey={undefined}
            >
            <SalesChecklistActionRegistrar agentId={selectedAgent} />
            <MessagePersistenceManager 
              agentId={selectedAgent} 
              threadId={agentThreadIds[selectedAgent]} 
            />
            <AgentActivityTracker
              agentId={selectedAgent}
              onToolsChange={stableToolsUpdater}
              onConnectedAgentsChange={stableConnectedUpdater}
            />
            <div className="h-full p-6 flex flex-col gap-4">
              <AgentActionPanel
                agentId={selectedAgent}
                agentName={currentAgent?.name ?? "Agent"}
              />
              <div className="flex-1 min-h-0 relative">
                <CopilotChat
                  className="h-full"
                  suggestions={suggestions}
                  labels={{
                    title: currentAgent?.name || "Agent",
                    initial: currentAgent?.initialMessage ?? defaultInitial,
                    placeholder: currentAgent?.placeholder ?? defaultPlaceholder
                  }}
                  instructions={buildAgentInstructions(currentAgent)}
                />
                {selectedAgent === "chief_analyst" && (
                  <AgentIconsMessageTracker agentId={selectedAgent} />
                )}
              </div>
            </div>
          </CopilotKit>
          )}
        </div>
      </div>
    </div>
  );
}

function buildAgentInstructions(agent?: (typeof AGENTS)[number]) {
  if (!agent) {
    return undefined;
  }

  const base = `You are the ${agent.name} agent. Stay within your domain: ${agent.description}`;
  const contextual = agent.id === "sales_insights"
    ? "When the user asks for an action plan, checklist, or prioritized next steps, call the frontend action named \"sales_campaign_checklist\" with focus and timeframe arguments when appropriate."
    : agent.id === "chief_analyst"
      ? "Use the frontend action \"sales_campaign_checklist\" when a cross-functional execution plan is required so the user sees the structured checklist."
      : "";

  return [base, contextual].filter(Boolean).join("\n\n");
}

// Component to persist and restore messages per agent thread
function MessagePersistenceManager({ agentId, threadId }: { agentId: AgentId; threadId: string }) {
  const { messages } = useCopilotMessagesContext();
  
  // Intercept fetch requests to inject stored messages into loadAgentState queries
  useEffect(() => {
    if (typeof window === 'undefined') return;
    
    // Store original fetch
    const originalFetch = window.fetch;
    
    // Override fetch to intercept CopilotKit requests
    window.fetch = async function(...args) {
      const [url, options] = args;
      
      // Check if this is a request to our CopilotKit API route
      if (typeof url === 'string' && url.includes('/api/copilotkit')) {
        try {
          // Try to read stored messages from localStorage
          const storageKey = `agentCouncilMessages-${agentId}-${threadId}`;
          const stored = localStorage.getItem(storageKey);
          
          if (stored) {
            const storedMessages = JSON.parse(stored) as Array<{ role: string; content: string }>;
            
            // Check if this is a loadAgentState query by examining the request body
            if (options?.body) {
              try {
                const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
                if (body?.query?.includes('loadAgentState') && storedMessages.length > 0) {
                  // Add stored messages to request headers
                  const headers = new Headers(options.headers);
                  headers.set('x-stored-messages', JSON.stringify(storedMessages));
                  console.log(`[MessagePersistence] Injecting ${storedMessages.length} stored messages into loadAgentState request`);
                  
                  return originalFetch(url, { ...options, headers });
                }
              } catch (e) {
                // Body parsing failed, continue with original request
              }
            }
          }
        } catch (e) {
          console.warn('[MessagePersistence] Failed to inject stored messages:', e);
        }
      }
      
      // Call original fetch for all other requests
      return originalFetch.apply(this, args);
    };
    
    // Cleanup: restore original fetch
    return () => {
      window.fetch = originalFetch;
    };
  }, [agentId, threadId]);
  
  // Save messages to localStorage whenever they change
  useEffect(() => {
    if (!messages || !threadId || typeof window === 'undefined') return;
    
    try {
      const storageKey = `agentCouncilMessages-${agentId}-${threadId}`;
      // Store messages as JSON
      const messagesToStore = messages
        .filter(msg => {
          // Only store user and assistant messages
          const msgData = msg as unknown as Record<string, unknown>;
          const role = msgData.role;
          return role === 'user' || role === 'assistant';
        })
        .map(msg => {
          // Extract serializable message data
          const msgData = msg as unknown as Record<string, unknown>;
          return {
            role: msgData.role,
            content: msgData.content || '',
            // Store any other relevant fields
          };
        });
      
      if (messagesToStore.length > 0) {
        localStorage.setItem(storageKey, JSON.stringify(messagesToStore));
        console.log(`[MessagePersistence] ✅ Saved ${messagesToStore.length} messages for agent ${agentId}, thread ${threadId}`);
      }
    } catch (e) {
      console.warn('[MessagePersistence] Failed to save messages:', e);
    }
  }, [messages, agentId, threadId]);
  
  return null;
}

// Monitor streamed Copilot messages so the sidebar can surface recent tool usage.
function AgentActivityTracker({
  onToolsChange,
  onConnectedAgentsChange,
  agentId,
}: {
  onToolsChange: (tools: string[]) => void;
  onConnectedAgentsChange: (agents: string[]) => void;
  agentId: AgentId;
}) {
  const { messages } = useCopilotMessagesContext();
  
  // Filter messages by agent - only show messages for the current agent
  // This ensures each agent maintains its own conversation history
  const agentMessages = useMemo(() => {
    if (!messages) return [];
    
    // Filter messages that belong to this agent
    // We can identify agent messages by checking metadata or thread context
    return messages.filter((msg) => {
      // For now, we'll show all messages but this can be enhanced
      // to filter by agent metadata if available
      return true;
    });
  }, [messages, agentId]);

  useEffect(() => {
    // Use filtered agent messages instead of all messages
    const messagesToUse = agentMessages;
    
    if (!messagesToUse?.length) {
      onToolsChange([]);
      onConnectedAgentsChange([]);
      return;
    }

    const assistantMessages = (messagesToUse as AssistantMessageLike[]).filter((msg) => {
      const possible = msg as unknown as { role?: unknown };
      return possible?.role === "assistant";
    });
    if (!assistantMessages.length) {
      return;
    }

  const latest = assistantMessages[assistantMessages.length - 1] as AssistantMessageLike;
    const toolNames = new Set<string>();

    if (Array.isArray(latest?.toolCalls)) {
      latest.toolCalls.forEach((call: ToolCallLike) => {
        const name = call?.function?.name ?? call?.name;
        if (typeof name === "string" && name.trim().length > 0) {
          toolNames.add(name.trim());
        }
      });
    }

    const metadata = extractMetadata(latest);
    const metadataTools = getArrayFromMetadata(metadata, ["tools_used", "toolsUsed", "tools"]);
    metadataTools.forEach((tool) => {
      if (tool) {
        toolNames.add(tool);
      }
    });

    const connected = getArrayFromMetadata(metadata, ["connected_agents", "connectedAgents", "agents"]);

    onToolsChange(Array.from(toolNames).slice(0, 6));
    onConnectedAgentsChange(connected.slice(0, 6));
  }, [agentMessages, onToolsChange, onConnectedAgentsChange]);

  return null;
}

function extractMetadata(message: AssistantMessageLike): Record<string, unknown> | null {
  const candidates: unknown[] = [message?.metadata];

  const state = message?.state;
  if (isRecord(state)) {
    candidates.push(state.metadata);
    if (isRecord(state.agui)) {
      candidates.push(state.agui.metadata);
    }
  }

  if (isRecord(message?.rawMessage)) {
    candidates.push(message.rawMessage.metadata);
  }
  if (isRecord(message?.raw)) {
    candidates.push(message.raw.metadata);
  }
  if (isRecord(message?.extensions)) {
    candidates.push(message.extensions.metadata);
  }

  // Try to get metadata from client-side store (matched by content)
  if (typeof window !== 'undefined') {
    const store = (window as any).__agentCouncilMetadataStore;
    if (store && store instanceof Map) {
      const content = (message as unknown as { content?: string })?.content;
      if (content) {
        // Clean content (remove script tags if any)
        const cleanContent = content.replace(/<script[^>]*>.*?<\/script>/gi, '').trim();
        
        // Try multiple matching strategies
        // Strategy 1: Exact content hash match
        const contentHash = cleanContent.substring(0, 100);
        if (store.has(contentHash)) {
          candidates.push(store.get(contentHash));
        }
        
        // Strategy 2: Try matching with first 50 chars
        const shortHash = cleanContent.substring(0, 50);
        if (store.has(shortHash)) {
          candidates.push(store.get(shortHash));
        }
        
        // Strategy 3: Try all stored keys and find best match
        let bestMatch: { key: string; meta: unknown; score: number } | null = null;
        for (const [key, meta] of store.entries()) {
          const keyContent = key.replace(/<script[^>]*>.*?<\/script>/gi, '').trim();
          // Calculate match score
          let score = 0;
          if (cleanContent.includes(keyContent) || keyContent.includes(cleanContent.substring(0, 50))) {
            score = Math.min(cleanContent.length, keyContent.length);
          }
          // Also try matching first words
          const contentWords = cleanContent.split(/\s+/).slice(0, 5).join(' ');
          const keyWords = keyContent.split(/\s+/).slice(0, 5).join(' ');
          if (contentWords === keyWords) {
            score = Math.max(score, 100);
          }
          
          if (score > 0 && (!bestMatch || score > bestMatch.score)) {
            bestMatch = { key, meta, score };
          }
        }
        
        if (bestMatch && bestMatch.score > 20) {
          candidates.push(bestMatch.meta);
        }
      }
    }
  }

  for (const candidate of candidates) {
    if (!candidate) {
      continue;
    }

    if (typeof candidate === "string") {
      try {
        return JSON.parse(candidate) as Record<string, unknown>;
      } catch {
        continue;
      }
    }

    if (isRecord(candidate)) {
      return candidate;
    }
  }

  return null;
}

function getArrayFromMetadata(metadata: Record<string, unknown> | null, keys: string[]) {
  if (!metadata) {
    return [] as string[];
  }

  for (const key of keys) {
    const value = metadata[key];
    if (Array.isArray(value)) {
      return value.filter((item) => typeof item === "string");
    }
    if (typeof value === "string") {
      return value.split(/[,\n]/).map((item) => item.trim()).filter(Boolean);
    }
  }

  return [] as string[];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

// Global store for metadata (client-side only)
// This stores metadata keyed by message content hash for matching
if (typeof window !== 'undefined') {
  (window as any).__agentCouncilMetadataStore = (window as any).__agentCouncilMetadataStore || new Map();
}

// Component to track messages and inject agent icons below assistant messages
function AgentIconsMessageTracker({ agentId }: { agentId: AgentId }) {
  const { messages: allMessages } = useCopilotMessagesContext();
  
  // Filter messages for current agent only
  // For now, we'll use all messages since CopilotKit manages threads
  // In the future, we can filter by agent metadata if available
  const messages = allMessages;
  
  // Extract metadata from script tags in rendered messages
  useEffect(() => {
    const extractMetadataFromDOM = () => {
      if (typeof window === 'undefined') return;
      
      const store = (window as any).__agentCouncilMetadataStore;
      if (!store) return;
      
      // Find all script tags with metadata
      const metadataScripts = document.querySelectorAll('script[data-agent-council-metadata]');
      metadataScripts.forEach((script) => {
        try {
          const metadata = JSON.parse(script.textContent || '{}');
          // Find the parent message container
          let parent = script.parentElement;
          while (parent && (!parent.textContent || (parent.textContent || '').trim().length < 50)) {
            parent = parent.parentElement;
          }
          if (parent) {
            // Get content without the script tag
            const content = (parent.textContent || '').replace(/<script[^>]*>.*?<\/script>/gi, '').trim();
            if (content.length > 50) {
              // Store with multiple keys for better matching
              const contentHash100 = content.substring(0, 100);
              const contentHash50 = content.substring(0, 50);
              const firstWords = content.split(/\s+/).slice(0, 5).join(' ');
              
              store.set(contentHash100, metadata);
              store.set(contentHash50, metadata);
              store.set(firstWords, metadata);
              
              console.log('[AgentIcons] Extracted and stored metadata from DOM, content preview:', content.substring(0, 50));
              // Remove the script tag after extracting
              script.remove();
            }
          }
        } catch (e) {
          console.warn('[AgentIcons] Failed to parse metadata from script tag:', e);
        }
      });
    };
    
    // Extract on mount and when messages change
    extractMetadataFromDOM();
    
    // Also watch for new script tags
    const observer = new MutationObserver(() => {
      extractMetadataFromDOM();
    });
    
    observer.observe(document.body, {
      childList: true,
      subtree: true
    });
    
    return () => observer.disconnect();
  }, [messages]);
  
  useEffect(() => {
    if (agentId !== "chief_analyst") return;
    
    const updateIcons = () => {
      // Find all assistant message containers in CopilotKit
      // Try multiple selectors to find the chat area
      const chatArea = document.querySelector('[class*="copilotKit"]') || 
                      document.querySelector('[class*="CopilotKit"]') ||
                      document.querySelector('[class*="chat"]') ||
                      document.querySelector('[class*="Chat"]') ||
                      document.querySelector('main') ||
                      document.body;
      
      if (!chatArea) {
        console.log('[AgentIcons] Chat area not found');
        return;
      }
      
      // Find all copilotKitMessageControls divs - this is where we inject the icons
      const messageControls = Array.from(chatArea.querySelectorAll('div.copilotKitMessageControls, div[class*="MessageControls"]'));
      
      console.log('[AgentIcons] Found', messageControls.length, 'message controls');
      
      // Get actual assistant messages from CopilotKit context
      const assistantMessageData = messages?.filter((m) => {
        const possible = m as unknown as { role?: unknown };
        return possible?.role === "assistant";
      }) || [];
      
      // For each message control, inject icons
      messageControls.forEach((controlDiv, index) => {
        try {
          // Skip if already processed
          if (controlDiv.querySelector('.agent-icons-indicator')) {
            return;
          }
          
          // Find the corresponding message (match by index or find the closest assistant message)
          const message = assistantMessageData[index] || assistantMessageData[assistantMessageData.length - 1];
          
          if (!message) {
            // Still show icons even without message data (for Chief Analyst)
            console.log('[AgentIcons] No message data for control', index, '- showing default icons');
          }
          
          // Extract metadata if available
          const metadata = message ? extractMetadata(message as AssistantMessageLike) : null;
          
          console.log('[AgentIcons] Processing control', index, 'metadata:', metadata ? 'found' : 'not found');
          
          const connectedAgents = metadata ? getArrayFromMetadata(metadata, [
            "connected_agents",
            "connectedAgents",
            "agents",
          ]) : [];
          
          const agentsToShow: AgentId[] = [
            "chief_analyst",
            "sales_insights",
            "dealer_performance",
            "inventory_ops",
          ];
          
          // Normalize connected agent IDs (handle various formats)
          const normalizedConnected = connectedAgents
            .map((id) => {
              if (typeof id !== "string") return "";
              return id.toLowerCase().trim();
            })
            .filter((id) => id.length > 0);
          
          // Create icon container
          const iconContainer = document.createElement('div');
          iconContainer.className = 'agent-icons-indicator flex items-center justify-end gap-2 px-4 py-2 mt-1';
          
          agentsToShow.forEach((aid) => {
            try {
              const agent = AGENTS.find((a) => a.id === aid);
              if (!agent) return; // Skip if agent not found
              
              const isCalled =
                aid === "chief_analyst" ||
                normalizedConnected.includes(aid.toLowerCase());
              
              const iconDiv = document.createElement('div');
              iconDiv.className = `relative flex items-center justify-center w-8 h-8 rounded-lg transition-all duration-200 cursor-pointer ${
                isCalled
                  ? "bg-green-100 border-2 border-green-500 shadow-md scale-110"
                  : "bg-gray-100 border border-gray-300 opacity-50"
              }`;
              iconDiv.title = agent.name || aid;
              
              // Safely set innerHTML with agent icon
              const iconSpan = document.createElement('span');
              iconSpan.className = 'text-lg';
              iconSpan.textContent = agent.icon || '?';
              iconDiv.appendChild(iconSpan);
              
              if (isCalled) {
                const indicator = document.createElement('div');
                indicator.className = 'absolute -top-1 -right-1 w-3 h-3 bg-green-500 rounded-full border-2 border-white';
                iconDiv.appendChild(indicator);
              }
              
              iconContainer.appendChild(iconDiv);
            } catch (err) {
              console.warn('Error rendering agent icon:', aid, err);
            }
          });
          
          // Always append icons (they'll be grayed out if not called)
          if (iconContainer.children.length > 0) {
            controlDiv.appendChild(iconContainer);
            console.log('[AgentIcons] ✅ Added icons to control', index, 'connected agents:', normalizedConnected);
          }
        } catch (err) {
          console.warn('[AgentIcons] ❌ Error processing control:', index, err);
        }
      });
    };
    
    // Debounce updates to avoid excessive re-renders
    let updateTimer: NodeJS.Timeout | null = null;
    const debouncedUpdate = () => {
      if (updateTimer) clearTimeout(updateTimer);
      updateTimer = setTimeout(updateIcons, 200);
    };
    
    // Initial update with longer delay to ensure DOM is ready and metadata is attached
    const initialTimer = setTimeout(() => {
      console.log('[AgentIcons] 🔍 Initial update - agentId:', agentId, 'messages:', messages?.length);
      updateIcons();
    }, 2000); // Increased delay to wait for metadata to be attached
    
    // Also update when messages change (new response arrives)
    const messagesTimer = setTimeout(() => {
      if (messages && messages.length > 0) {
        console.log('[AgentIcons] 📨 Messages updated, checking for metadata');
        updateIcons();
      }
    }, 3000); // Wait a bit longer for metadata to be attached to new messages
    
    // Watch for new messages with debouncing
    const observer = new MutationObserver(() => {
      debouncedUpdate();
    });
    
    const chatArea = document.querySelector('[class*="copilotKit"]') || 
                    document.querySelector('[class*="chat"]') ||
                    document.body;
    
    if (chatArea) {
      observer.observe(chatArea, {
        childList: true,
        subtree: true,
      });
    }
    
    return () => {
      if (initialTimer) clearTimeout(initialTimer);
      if (messagesTimer) clearTimeout(messagesTimer);
      if (updateTimer) clearTimeout(updateTimer);
      observer.disconnect();
    };
  }, [messages, agentId]);
  
  return null;
}

function AgentActionPanel({ agentId, agentName }: { agentId: AgentId; agentName: string }) {
  const { appendMessage, isLoading } = useCopilotChat();
  const [pendingActionId, setPendingActionId] = useState<string | null>(null);

  const actions = useMemo<QuickAction[]>(() => {
    return AGENT_ACTIONS[agentId] ?? [];
  }, [agentId]);

  const triggerAction = useCallback(async (action: QuickAction) => {
    setPendingActionId(action.id);
    try {
      const prompt = action.prompt;
      await appendMessage(
        new TextMessage({
          role: MessageRole.User,
          content: prompt,
        })
      );
    } finally {
      setPendingActionId(null);
    }
  }, [appendMessage]);

  if (!actions.length) {
    return null;
  }

  return (
    <div className="bg-white border border-gray-200 rounded-2xl shadow-sm p-4">
      <div className="flex items-center justify-between mb-3">
        <div>
          <h3 className="text-sm font-semibold text-gray-700">{agentName} Quick Actions</h3>
          <p className="text-xs text-gray-500">Jump-start common workflows with a single click.</p>
        </div>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        {actions.map((action) => (
          <button
            key={action.id}
            onClick={() => triggerAction(action)}
            disabled={isLoading || pendingActionId === action.id}
            className={`text-left p-4 rounded-xl border border-dashed transition-all duration-200 ${
              pendingActionId === action.id
                ? "border-blue-500 bg-blue-50 text-blue-900"
                : "border-gray-300 bg-gray-50 hover:border-blue-400 hover:bg-white"
            } ${isLoading ? "opacity-70 cursor-not-allowed" : ""}`}
          >
            <div className="font-semibold text-sm mb-1">{action.title}</div>
            <p className="text-xs text-gray-600 leading-relaxed">{action.description}</p>
            <div className="text-xs text-gray-400 mt-2">Auto-prompts the {agentName} agent.</div>
          </button>
        ))}
      </div>
    </div>
  );
}

// Register a Copilot action that renders a structured checklist in the chat transcript.
function SalesChecklistActionRegistrar({ agentId }: { agentId: AgentId }) {
  useCopilotAction(
    {
      name: "sales_campaign_checklist",
      description: "Render a prioritized checklist that keeps owners and due dates visible to the user.",
      parameters: [
        {
          name: "focus",
          type: "string",
          required: false,
          description: "Optional focus area or initiative name provided by the user.",
        },
        {
          name: "timeframe",
          type: "string",
          required: false,
          description: "Campaign window or due horizon (e.g. \"next 30 days\").",
        },
      ],
      handler: async ({ focus, timeframe }) => {
        const res = await fetch("/api/actions/sales-checklist", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ focus, timeframe, agentId }),
        });

        if (!res.ok) {
          const errorText = await res.text();
          throw new Error(errorText || "Failed to generate checklist");
        }

        const payload = (await res.json()) as SalesChecklistResult;
        return payload;
      },
      render: ({ args, result, status }) => (
        <SalesChecklistCard
          focus={args?.focus as string | undefined}
          timeframe={args?.timeframe as string | undefined}
          status={status}
          result={result as SalesChecklistResult | undefined}
        />
      ),
    },
    [agentId]
  );

  return null;
}

type SalesChecklistResult = {
  title?: string;
  summary?: string;
  items?: Array<{ label: string; owner?: string; due?: string; status?: string }>;
  raw?: string;
};

function SalesChecklistCard({
  focus,
  timeframe,
  status,
  result,
}: {
  focus?: string;
  timeframe?: string;
  status: string;
  result?: SalesChecklistResult;
}) {
  const parsed = useMemo(() => normalizeChecklist(result), [result]);

  return (
    <div className="bg-white border border-blue-200 rounded-2xl p-4 shadow-sm">
      <div className="flex justify-between items-start gap-4">
        <div>
          <h4 className="text-sm font-semibold text-blue-900">{parsed.title || "Execution Checklist"}</h4>
          <p className="text-xs text-blue-700 mt-1">
            {(focus || timeframe)
              ? [focus ? `Focus: ${focus}` : null, timeframe ? `Horizon: ${timeframe}` : null].filter(Boolean).join(" • ")
              : "Prioritized follow-up actions"}
          </p>
        </div>
        <span className="text-xs font-semibold uppercase tracking-wide text-blue-600">
          {statusLabel(status)}
        </span>
      </div>

      <ul className="mt-3 space-y-2">
        {parsed.items.length ? (
          parsed.items.map((item, index) => (
            <li key={`${item.label}-${index}`} className="bg-blue-50 border border-blue-100 rounded-xl p-3">
              <div className="text-sm font-medium text-blue-900">{item.label}</div>
              <div className="text-xs text-blue-700 mt-1 flex flex-wrap gap-2">
                {item.owner && <span>Owner: {item.owner}</span>}
                {item.due && <span>Due: {item.due}</span>}
                {item.status && <span>Status: {item.status}</span>}
              </div>
            </li>
          ))
        ) : (
          <li className="text-xs text-blue-700">Waiting for the agent to populate checklist details…</li>
        )}
      </ul>

      {parsed.summary && (
        <p className="text-xs text-blue-700 mt-3 leading-relaxed">{parsed.summary}</p>
      )}
    </div>
  );
}

function statusLabel(status: string) {
  switch (status) {
    case "executing":
      return "Running";
    case "succeeded":
      return "Ready";
    case "failed":
      return "Error";
    default:
      return "Updating";
  }
}

function normalizeChecklist(result?: SalesChecklistResult) {
  if (!result) {
    return { title: undefined, summary: undefined, items: [] as Array<{ label: string; owner?: string; due?: string; status?: string }> };
  }

  if (result.items && Array.isArray(result.items) && result.items.length) {
    return {
      title: result.title,
      summary: result.summary,
      items: result.items.map((item) => ({
        label: item.label,
        owner: item.owner,
        due: item.due,
        status: item.status,
      })),
    };
  }

  const items: Array<{ label: string; owner?: string; due?: string; status?: string }> = [];
  const raw = result.raw || result.summary || "";
  if (typeof raw === "string" && raw.trim().length > 0) {
    const candidateLines = raw.split(/\r?\n|•|\-/).map((line) => line.trim()).filter(Boolean);
    candidateLines.forEach((line) => {
      items.push({ label: line });
    });
  }

  return {
    title: result.title,
    summary: result.summary,
    items,
  };
}
