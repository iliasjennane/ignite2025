# Testing Blazor UI → API Integration

## ✅ **Setup Complete!**

The Blazor UI is now wired to call your API when you click Send.

## 🔄 **How It Works**

```
User Types Message in Blazor
        ↓
Clicks "Send" Button
        ↓
Blazor calls ApiAgentsClient
        ↓
HTTP POST → https://localhost:7213/api/chat/send
        ↓
API receives message & processes
        ↓
API returns response
        ↓
Blazor displays response in chat
```

## 🧪 **Test It Now!**

### Step 1: Open Blazor App
```
https://localhost:7263
```

### Step 2: Navigate to an Agent Page
- Click on "Sales Insights Agent" (or any other agent)
- Or go directly: https://localhost:7263/agents/sales-insights

### Step 3: Send a Message
1. Type a message in the text box (or click a sample query chip)
2. Click the **Send** button (or press Enter)
3. Watch the agent respond!

## 📊 **What You'll See**

### In the Browser:
- Your message appears on the right (blue bubble)
- Loading indicator shows "Agent is analyzing..."
- Agent response appears on the left (white bubble)

### In the API Logs:
```bash
tail -f /tmp/api.log
```

You'll see:
```
info: Program[0]
      Received chat request: Your message here
info: Program[0]
      Agent ID: your-agent-id, Endpoint: https://your-foundry-endpoint.azure.com
```

### In the Blazor Logs:
```bash
tail -f /tmp/blazor.log
```

You'll see hot reload messages as you edit files.

## 🔍 **Current Response**

Right now, the API returns a **placeholder** response:
```
Echo from agent your-agent-id: [Your message]

Note: This is a placeholder. Configure Azure AI Foundry credentials 
and implement the actual agent invocation.
```

## 🎯 **Available Agent Pages**

All these pages are wired to the API:

1. **Sales Insights** - `/agents/sales-insights`
2. **Ops Risk** - `/agents/ops-risk`  
3. **Doc Evidence** - `/agents/doc-evidence`
4. **Market Context** - `/agents/market-context`
5. **Data Gateway** - `/agents/data-gateway`
6. **Chief Analyst** - `/agents/chief-analyst`

## 🔧 **Configuration**

The setting is in `AgentCouncil.BlazorWasm/wwwroot/appsettings.Development.json`:

```json
{
  "UseApiClient": true,  ← This enables API mode
  "ApiBaseUrl": "https://localhost:7213"
}
```

**Toggle this to switch between:**
- `true` = Routes through your API
- `false` = Calls Azure agents directly

## 🐛 **Troubleshooting**

### If messages don't send:

1. **Check API is running:**
   ```bash
   curl -k https://localhost:7213/api/chat/send \
     -H 'Content-Type: application/json' \
     -d '{"message":"test"}'
   ```

2. **Check browser console (F12):**
   - Look for CORS errors
   - Look for network errors

3. **Check API logs:**
   ```bash
   tail -f /tmp/api.log
   ```

4. **Verify configuration:**
   - Open browser DevTools → Network tab
   - Send a message
   - Look for POST to `localhost:7213/api/chat/send`
   - Should return status 200

## 🚀 **Next Steps**

To connect to real Azure AI Foundry agents:

1. Edit `AgentCouncil.API/appsettings.Development.json`
2. Set your Azure AI Foundry credentials:
   ```json
   {
     "AzureAIFoundryProjectEndpoint": "YOUR_ENDPOINT",
     "AzureAIFoundryAgentId": "YOUR_AGENT_ID"
   }
   ```
3. Implement the agent invocation logic in `AgentCouncil.API/Program.cs`
4. Replace the placeholder response with actual agent calls

## 💡 **Pro Tip**

Keep both logs open in separate terminals:

**Terminal 1:**
```bash
tail -f /tmp/api.log
```

**Terminal 2:**
```bash
tail -f /tmp/blazor.log
```

This way you can see the full request/response flow!


