# Agent Council

A demo application showcasing Azure AI Foundry agents with a .NET 10 minimal API backend and Blazor WebAssembly frontend.

## Projects

- **AgentCouncil.API** - .NET 10 Minimal API (`https://localhost:7213`)
- **AgentCouncil.BlazorWasm** - Blazor WASM Frontend (`https://localhost:7263`)

## Agent Pages

The application includes 5 specialized agent pages, each mapped to an Azure AI Foundry agent:

| Page | Agent Name | Description |
|------|------------|-------------|
| **Chief Analyst** | `chief_analyst` | Orchestrates all other agents to provide comprehensive insights |
| **Sales Insights** | `sales_analyst` | Sales performance and revenue analysis |
| **Ops Risk** | `ops_analyst` | Operational risks and supply chain analysis |
| **Market Context** | `market_analyst` | Market trends and competitive analysis |
| **Data Gateway** | `data_analyst` | Structured data access and visualizations |

All pages share a consistent layout with:
- User messages aligned right with 4px gap to icon
- Assistant messages aligned left with 8px gap
- Messages constrained to 80% max width
- Real-time tool usage tracking

## Quick Start

### ⚡ Simple Way (Recommended)

```bash
./start.sh    # Start both projects
./stop.sh     # Stop everything
```

### 🔧 Advanced (Interactive)

```bash
./run.sh          # Start both projects (default)
./run.sh test     # Run integration tests
./run.sh start    # Start API only
./run.sh stop     # Stop all
./run.sh help     # Show help
```

## URLs

- **API**: https://localhost:7213
- **API Docs (Scalar)**: https://localhost:7213/scalar/v1
- **Blazor**: https://localhost:7263

## View Logs

```bash
tail -f /tmp/api.log      # API logs
tail -f /tmp/blazor.log   # Blazor logs
```

## Configuration

### API Settings
`AgentCouncil.API/appsettings.Development.json`:
```json
{
  "AzureAIFoundryProjectEndpoint": "YOUR_ENDPOINT",
  "AzureAIFoundryAgentId": "YOUR_AGENT_ID",
  "BlazorOrigin": "https://localhost:7263"
}
```

### Blazor Settings
`AgentCouncil.BlazorWasm/wwwroot/appsettings.Development.json`:
```json
{
  "UseApiClient": true,  // true = use API, false = direct calls
  "ApiBaseUrl": "https://localhost:7213"
}
```

## Testing the Integration

1. **Run the test:**
   ```bash
   ./run.sh test
   ```

2. **Enable API mode in Blazor:**
   Set `UseApiClient: true` in Blazor's `appsettings.Development.json`

3. **Rebuild Blazor:**
   ```bash
   cd AgentCouncil.BlazorWasm && dotnet build
   ```

4. **Start both and test:**
   ```bash
   ./run.sh both
   ```

5. **Open browser:** `https://localhost:7263`

6. **Watch both consoles** for "Received chat request" messages when you submit queries

## What the Script Does

### `./run.sh` or `./run.sh both`
- Stops any existing instances
- Starts API on port 7213
- Starts Blazor on port 7263
- Handles Ctrl+C gracefully

### `./run.sh test`
- Stops existing instances
- Starts API
- Runs integration tests:
  - Valid message test
  - Empty message validation
  - CORS headers
- Shows results
- Gives you options to continue

### `./run.sh stop`
- Finds all running processes on ports 7213, 5068, 7263, 5033
- Stops them all
- Clean exit

## Features

- ✅ .NET 10 minimal API with route groups
- ✅ Swagger/OpenAPI documentation
- ✅ CORS configuration for Blazor WASM
- ✅ Azure AI Foundry integration with real agent orchestration
- ✅ 5 specialized agent pages (Chief Analyst, Sales, Ops, Market, Data)
- ✅ Real-time tool usage tracking from Azure AI Foundry
- ✅ OpenTelemetry tracing with Azure Monitor
- ✅ Consistent UI layout across all agent pages
- ✅ Configurable client routing (Direct or API)
- ✅ Hot reload enabled (dotnet watch)
- ✅ One script to rule them all

## Troubleshooting

**SSL Certificate Errors:**
```bash
dotnet dev-certs https --trust
```

**Port Already in Use:**
```bash
./run.sh stop
```

**Check if running:**
```bash
lsof -i :7213  # API
lsof -i :7263  # Blazor
```

## Next Steps

1. Configure Azure AI Foundry credentials in `appsettings.Development.json`
2. Implement actual agent logic in `AgentCouncil.API/Program.cs` (marked with TODO)
3. Enable API mode: Set `UseApiClient: true` in Blazor config
4. Test with `./run.sh test`

## Architecture

```
┌─────────────────┐
│  Blazor WASM    │  User Interface
│  localhost:7263 │  
└────────┬────────┘
         │ HTTP POST
         │ (when UseApiClient: true)
         ↓
┌─────────────────┐
│  .NET 10 API    │  Backend / Orchestration
│  localhost:7213 │  
└────────┬────────┘
         │
         ↓
    Azure AI Foundry
```

## License

Demo project for Azure AI Foundry integration.

## Demo Documentation

The repository includes comprehensive demo documentation for Ignite 2025:

- **`DEMO_RUNBOOK.md`** - Complete 25-minute demo script with talking points, scenarios, and troubleshooting
- **`DEMO_SCENARIOS.md`** - Quick reference with 40+ pre-tested queries organized by scenario
- **`DEMO_PREPARATION_CHECKLIST.md`** - Comprehensive pre-demo checklist (1 week, 3 days, 1 day, day-of)
- **`DEMO_QUICK_START.md`** - Quick start guide for immediate demo execution

**For first-time demo setup**, start with `DEMO_PREPARATION_CHECKLIST.md`.  
**For demo execution**, use `DEMO_RUNBOOK.md` as your script.  
**For quick reference**, use `DEMO_SCENARIOS.md` for queries.

---

## Ignite2025 Demo Data Generator

The repository includes a Fabric-focused synthetic data notebook: `Ignite2025_Demo_Data_Generator.ipynb`.

### Purpose
Generates a reduced-scale automotive dataset (≈350K sales rows) with narrative anomalies for live demos and Microsoft Fabric Data Agents:
- Azure Motors EV SUV campaign uplift (Jun–Sep 2025)
- Logistics delay (West & Pacific NW inbound inventory reduction Aug–Sep 2025)
- Dealer performance concentration & customer segmentation

### Tables (Delta)
`dates`, `business_events`, `models`, `dealers`, `customers`, `incentives`, `regional_market_signals`, `inventory`, `car_sales`, `dealer_performance_monthly`, `model_performance_monthly`, `customer_feedback`, `semantic_documents`, `business_questions_answers`, `table_metadata`, `agent_instructions`.

Each table uses human-friendly names so Fabric Data Agents can parse schemas directly (no SQL views required). Pre-aggregated performance tables simplify natural language queries (e.g., "dealer performance last quarter").

### Key Columns & Signals
- Flags: `is_campaign_period`, `is_delay_month`, `stockout_flag`, `promotion_applied_flag`
- Descriptive text: `model_marketing_desc`, `dealer_profile_text`, `promo_summary_text`, `feedback_text`, `event_narrative`
- KPIs: `total_units`, `total_revenue`, `total_profit`, `margin_pct`, `avg_discount_pct`
- Quality & lineage: `quality_score`, `data_version`, `ingestion_timestamp`

### Example Q&A Guidance
The `business_questions_answers` table stores curated NL question → SQL answer pairs (validated) to guide Fabric Data Agent query generation (e.g., campaign uplift, logistics impact, dealer rankings, sentiment distribution). These are analogous to "example queries" described in Fabric Data Agent guidance.

### Agent Preparation Checklist
1. Select only relevant tables (avoid exposing staging).
2. Include `business_questions_answers` & `table_metadata` for richer context.
3. Provide agent instructions referencing table roles (e.g., prefer `dealer_performance_monthly` for profit trends).
4. Validate example SQL before ingestion.
5. Keep deterministic seed for reproducibility.

### Extending
You can expand with additional anomaly events, more sentiment categories, or micro-scale variant (reduce constants) by editing the configuration cell in the notebook.

### Runtime
Designed to complete in a few minutes on small Fabric compute; largest cost centers are `car_sales` and `inventory` generation loops.

---
