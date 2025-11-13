'use client';

import { CopilotKit } from "@copilotkit/react-core";
import { CopilotChat } from "@copilotkit/react-ui";
import "@copilotkit/react-ui/styles.css";
import { useCallback, useEffect, useMemo, useState } from "react";
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
      'Which dealer tiers drove EV SUV growth in the campaign months?'
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
      'What was the total revenue for Azure Motors EV SUVs during June–Sep 2025?',
      'Compare campaign revenue vs baseline for Azure Motors EV SUVs.',
      'Which region led Azure Motors EV SUV revenue during the campaign?'
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
      'Which top 10 dealers had the highest revenue in Q3 2025?',
      'Compare average dealer profit between West and Pacific NW in Q3 2025.',
      'How did Tier 1 dealers perform versus Tier 2 and Tier 3 during the campaign?'
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
      'How did incoming inventory change in West and Pacific NW during Aug–Sep 2025?',
      'Which dealers experienced stockouts in Pacific NW during Aug–Sep 2025?',
      'Has incoming inventory recovered in the West region by Sep 2025?'
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
          <div className="flex items-center gap-4">
            <div className={`${currentAgent?.color} w-16 h-16 rounded-2xl flex items-center justify-center text-3xl shadow-lg`}>
              {currentAgent?.icon}
            </div>
            <div>
              <h2 className="text-2xl font-bold text-gray-900">{currentAgent?.name}</h2>
              <p className="text-sm text-gray-600">{currentAgent?.description}</p>
            </div>
          </div>
        </div>

        {/* CopilotKit Chat Interface */}
        <div className="flex-1 bg-gray-100">
          <CopilotKit
            runtimeUrl="/api/copilotkit"
            properties={{ agentId: selectedAgent }}
            publicApiKey={undefined}
          >
            <SalesChecklistActionRegistrar agentId={selectedAgent} />
            <AgentActivityTracker
              onToolsChange={stableToolsUpdater}
              onConnectedAgentsChange={stableConnectedUpdater}
            />
            <div className="h-full p-6 flex flex-col gap-4">
              <AgentActionPanel
                agentId={selectedAgent}
                agentName={currentAgent?.name ?? "Agent"}
              />
              <div className="flex-1 min-h-0">
                <CopilotChat
                  key={selectedAgent}
                  className="h-full"
                  suggestions={suggestions}
                  labels={{
                    title: currentAgent?.name || "Agent",
                    initial: currentAgent?.initialMessage ?? defaultInitial,
                    placeholder: currentAgent?.placeholder ?? defaultPlaceholder
                  }}
                  instructions={buildAgentInstructions(currentAgent)}
                />
              </div>
            </div>
          </CopilotKit>
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

// Monitor streamed Copilot messages so the sidebar can surface recent tool usage.
function AgentActivityTracker({
  onToolsChange,
  onConnectedAgentsChange,
}: {
  onToolsChange: (tools: string[]) => void;
  onConnectedAgentsChange: (agents: string[]) => void;
}) {
  const { messages } = useCopilotMessagesContext();

  useEffect(() => {
    if (!messages?.length) {
      onToolsChange([]);
      onConnectedAgentsChange([]);
      return;
    }

    const assistantMessages = (messages as AssistantMessageLike[]).filter((msg) => {
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
  }, [messages, onToolsChange, onConnectedAgentsChange]);

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
