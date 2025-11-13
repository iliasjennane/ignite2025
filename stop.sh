#!/bin/bash
# Stop script - Stop all services

echo "🛑 Stopping Agent Council services..."

# Stop from PID files
if [ -f /tmp/agentcouncil-api.pid ]; then
    kill $(cat /tmp/agentcouncil-api.pid) 2>/dev/null && echo "   ✓ API stopped"
    rm /tmp/agentcouncil-api.pid
fi

if [ -f /tmp/agentcouncil-ui.pid ]; then
    kill $(cat /tmp/agentcouncil-ui.pid) 2>/dev/null && echo "   ✓ CopilotKit UI stopped"
    rm /tmp/agentcouncil-ui.pid
fi

# Cleanup ports forcefully
lsof -ti:7213 | xargs kill -9 2>/dev/null
lsof -ti:3000 | xargs kill -9 2>/dev/null

echo ""
echo "✅ All services stopped"
