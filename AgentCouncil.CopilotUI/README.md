# Agent Council - CopilotKit UI

Modern Next.js frontend for Agent Council using CopilotKit components.

## What This Is

This is a **modern UI replacement** for the Blazor WASM frontend. It uses:
- **Next.js 15** - React framework
- **CopilotKit** - Pre-built AI chat components  
- **Tailwind CSS** - Styling
- **TypeScript** - Type safety

## Architecture

```
┌─────────────────┐         ┌──────────────────┐         ┌─────────────────────┐
│  CopilotKit UI  │────────▶│  .NET API        │────────▶│  Azure AI Foundry   │
│  (Next.js)      │  HTTP   │  AG UI Protocol  │  SDK    │  Agents             │
│  Port 3000      │         │  Port 7213       │         │                     │
└─────────────────┘         └──────────────────┘         └─────────────────────┘
```

## Quick Start

### First Time Setup

```bash
# From the agentcouncil root directory
npm install
```

### Run Development Server

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000)

## Features

✅ **Agent Switcher** - Toggle between all 7 specialist agents  
✅ **Real-time Chat** - Streaming responses from agents  
✅ **Tool Visualization** - See which tools agents use  
✅ **Multi-Agent Support** - Shows when Chief Analyst calls other agents  
✅ **Professional UI** - Pre-built CopilotKit components  

## Available Agents

| Agent | ID | Icon | Purpose |
|-------|----|----- |---------|
| Chief Analyst | `chief_analyst` | 🎯 | Executive orchestrator |
| Sales Insights | `sales_domain_expert` | 💰 | Sales analysis |
| Dealer Performance | `dealers_domain_expert` | 🏪 | Dealer analytics |
| Inventory Ops | `inventory_domain_expert` | 📦 | Inventory tracking |
| Car Models | `car_models_domain_expert` | 🚗 | Model insights |
| Customer Expert | `customers_domain_expert` | 👥 | Customer behavior |
| Incentives | `incentives_domain_expert` | 🎁 | Promotions |

## Configuration

Edit `.env.local`:

```bash
NEXT_PUBLIC_API_URL=https://localhost:7213
NODE_TLS_REJECT_UNAUTHORIZED=0  # Allow self-signed certs in dev
```

## Project Structure

```
app/
├── api/
│   └── copilotkit/
│       └── route.ts          # Proxy to .NET API
├── layout.tsx                # Root layout
├── page.tsx                  # Main chat interface
└── globals.css               # Global styles

```

## Development

### Prerequisites

- Node.js 18+
- npm 8+
- .NET API running on port 7213

### Build for Production

```bash
npm run build
npm start
```

### Lint

```bash
npm run lint
```

## How It Works

1. **User selects agent** in the sidebar
2. **User types message** in CopilotKit chat
3. **Next.js API route** forwards to .NET API (`/api/agui/chat`)
4. **.NET API** uses AG UI protocol to call Azure AI agent
5. **Response streams back** through the chain to CopilotKit UI

## Troubleshooting

### API Connection Errors

Ensure .NET API is running:
```bash
cd ../AgentCouncil.API
dotnet run
```

### Module Not Found Errors

```bash
npm install
```

### Port Already in Use

```bash
# Kill process on port 3000
lsof -ti:3000 | xargs kill -9
```

## Learn More

- [CopilotKit Documentation](https://docs.copilotkit.ai)
- [Next.js Documentation](https://nextjs.org/docs)
- [AG UI Protocol](https://docs.copilotkit.ai/microsoft-agent-framework)
