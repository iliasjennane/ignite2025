# Agent Council API Configuration

## Setup Instructions

### 1. Configure appsettings.json

Copy `appsettings.json.template` to `appsettings.json`:

```bash
cp appsettings.json.template appsettings.json
```

### 2. Add Your Azure AI Foundry Agent Configuration

Edit `appsettings.json` and replace the placeholder values:

```json
{
  "FoundryAgents": {
    "chief_analyst": {
      "Endpoint": "https://YOUR-RESOURCE.services.ai.azure.com/api/projects/YOUR-PROJECT",
      "AgentId": "asst_YOUR_AGENT_ID"
    },
    "sales_insights": {
      "Endpoint": "https://YOUR-RESOURCE.services.ai.azure.com/api/projects/YOUR-PROJECT",
      "AgentId": "asst_YOUR_AGENT_ID"
    },
    "dealer_performance": {
      "Endpoint": "https://YOUR-RESOURCE.services.ai.azure.com/api/projects/YOUR-PROJECT",
      "AgentId": "asst_YOUR_AGENT_ID"
    },
    "inventory_ops": {
      "Endpoint": "https://YOUR-RESOURCE.services.ai.azure.com/api/projects/YOUR-PROJECT",
      "AgentId": "asst_YOUR_AGENT_ID"
    }
  }
}
```

### 3. (Optional) Configure Application Insights

If you want telemetry, add your Application Insights connection string:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "YOUR_CONNECTION_STRING",
    "WorkspaceId": "YOUR_WORKSPACE_ID"
  }
}
```

### 4. Authentication

The API uses `DefaultAzureCredential` for authentication. Make sure you're logged into Azure CLI:

```bash
az login
```

### 5. Run the Application

```bash
cd ..
./start.sh
```

The API will be available at:
- HTTP: http://localhost:5068
- HTTPS: https://localhost:7213

## Security Notes

⚠️ **NEVER commit `appsettings.json` with real secrets to git!**

The `.gitignore` is configured to ignore this file. Keep your secrets local only.
