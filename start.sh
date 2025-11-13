#!/bin/bash
# Start script - API + CopilotKit UI

echo "🚀 Starting Agent Council with CopilotKit UI"
echo ""

# Kill any existing processes
echo "🧹 Cleaning up existing processes..."
lsof -ti:5068 | xargs kill -9 2>/dev/null
lsof -ti:7213 | xargs kill -9 2>/dev/null
lsof -ti:3000 | xargs kill -9 2>/dev/null

# Prefer the .NET 10 SDK if multiple installs exist
DOTNET_BIN=$(command -v dotnet)
if ! "$DOTNET_BIN" --list-sdks | grep -q '10\.' 2>/dev/null; then
	if [ -x /usr/local/share/dotnet/dotnet ]; then
		export DOTNET_ROOT="/usr/local/share/dotnet"
		export PATH="$DOTNET_ROOT:$PATH"
		DOTNET_BIN="/usr/local/share/dotnet/dotnet"
		echo "   ℹ️ Using .NET SDK from $DOTNET_BIN"
	fi
fi

# Start API
echo "📡 Starting .NET API..."
cd AgentCouncil.API
"$DOTNET_BIN" run > /tmp/agentcouncil-api.log 2>&1 &
API_PID=$!
echo "   API PID: $API_PID"

# Wait for API to be ready
echo "   Waiting for API to start..."
sleep 5

# Start CopilotKit UI
echo ""
echo "🎨 Starting CopilotKit UI..."
cd ../AgentCouncil.CopilotUI
npm run dev > /tmp/agentcouncil-ui.log 2>&1 &
COPILOT_PID=$!
echo "   CopilotKit PID: $COPILOT_PID"

# Save PIDs for stop script
echo "$API_PID" > /tmp/agentcouncil-api.pid
echo "$COPILOT_PID" > /tmp/agentcouncil-ui.pid

echo ""
echo "✅ All services started!"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📍 URLs:"
echo "   • CopilotKit UI:  http://localhost:3000"
echo "   • API:            https://localhost:7213"
echo "   • API Docs:       https://localhost:7213/scalar/v1"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "📊 View logs:"
echo "   tail -f /tmp/agentcouncil-api.log"
echo "   tail -f /tmp/agentcouncil-ui.log"
echo ""
echo "🛑 Stop services:"
echo "   ./stop.sh"
echo ""
