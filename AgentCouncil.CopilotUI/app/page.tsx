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
    description: 'Executive analytics orchestrator',
    color: 'bg-blue-600'
  },
  { 
    id: 'sales_domain_expert', 
    name: 'Sales Insights', 
    icon: '💰',
    description: 'Sales performance and revenue analysis',
    color: 'bg-green-600'
  },
  { 
    id: 'dealers_domain_expert', 
    name: 'Dealer Performance', 
    icon: '🏪',
    description: 'Dealer strength and regional analysis',
    color: 'bg-purple-600'
  },
  { 
    id: 'inventory_domain_expert', 
    name: 'Inventory Operations', 
    icon: '📦',
    description: 'Inventory health and supply chain',
    color: 'bg-orange-600'
  },
  { 
    id: 'car_models_domain_expert', 
    name: 'Car Models Expert', 
    icon: '🚗',
    description: 'Vehicle model insights and trends',
    color: 'bg-red-600'
  },
  { 
    id: 'customers_domain_expert', 
    name: 'Customer Expert', 
    icon: '👥',
    description: 'Customer analytics and behavior',
    color: 'bg-indigo-600'
  },
  { 
    id: 'incentives_domain_expert', 
    name: 'Incentives Expert', 
    icon: '🎁',
    description: 'Promotion and incentive analysis',
    color: 'bg-pink-600'
  },
];

export default function Home() {
  const [selectedAgent, setSelectedAgent] = useState(AGENTS[0].id);
  const [toolsUsed, setToolsUsed] = useState<string[]>([]);
  const [connectedAgents, setConnectedAgents] = useState<string[]>([]);
  
  const currentAgent = AGENTS.find(a => a.id === selectedAgent);

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
                className="h-full"
                labels={{
                  title: currentAgent?.name || "Agent",
                  initial: "How can I help you analyze your business data today?",
                  placeholder: "Ask me anything about sales, dealers, or inventory..."
                }}
              />
            </div>
          </CopilotKit>
        </div>
      </div>
    </div>
  );
}
