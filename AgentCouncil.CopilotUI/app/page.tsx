'use client';

import { CopilotKit } from "@copilotkit/react-core";
import { CopilotChat } from "@copilotkit/react-ui";
import "@copilotkit/react-ui/styles.css";
import { useState } from "react";

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

export default function Home() {
  const [selectedAgent, setSelectedAgent] = useState(AGENTS[0].id);
  const [toolsUsed, setToolsUsed] = useState<string[]>([]);
  const [connectedAgents, setConnectedAgents] = useState<string[]>([]);
  
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
            <div className="h-full p-6">
              <CopilotChat
                key={selectedAgent}
                className="h-full"
                suggestions={suggestions}
                labels={{
                  title: currentAgent?.name || "Agent",
                  initial: currentAgent?.initialMessage ?? defaultInitial,
                  placeholder: currentAgent?.placeholder ?? defaultPlaceholder
                }}
              />
            </div>
          </CopilotKit>
        </div>
      </div>
    </div>
  );
}
