#!/bin/bash

# Agent Council - Unified Management Script
# One script to rule them all: start, stop, test, build, status
# Usage: ./agentcouncil.sh [command]

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
PURPLE='\033[0;35m'
NC='\033[0m'

# Configuration
API_URL="https://localhost:7213"
BLAZOR_URL="https://localhost:7263"
API_DOCS_URL="https://localhost:7213/scalar/v1"
API_PORTS=(7213 5068)
BLAZOR_PORTS=(7263 5033)
MAX_WAIT_TIME=60
BUILD_TIMEOUT=120

# Log files
API_LOG="/tmp/api.log"
BLAZOR_LOG="/tmp/blazor.log"
STARTUP_LOG="/tmp/startup.log"

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

print_header() {
    echo ""
    print_status $CYAN "=========================================="
    print_status $CYAN "  $1"
    print_status $CYAN "=========================================="
    echo ""
}

# Function to check if port is in use
is_port_in_use() {
    local port=$1
    lsof -ti:$port >/dev/null 2>&1
}

# Function to get process info for port
get_port_process() {
    local port=$1
    lsof -ti:$port 2>/dev/null | head -1
}

# Enhanced cleanup function
cleanup_all() {
    print_status $YELLOW "🧹 Cleaning up existing processes..."
    
    local cleaned=0
    local total_cleaned=0
    
    # Kill processes by port (more precise)
    for port in "${API_PORTS[@]}" "${BLAZOR_PORTS[@]}"; do
        if is_port_in_use $port; then
            local pid=$(get_port_process $port)
            print_status $YELLOW "  Stopping process on port $port (PID: $pid)"
            kill -TERM $pid 2>/dev/null
            cleaned=1
            ((total_cleaned++))
        fi
    done
    
    # Wait for graceful shutdown
    if [ $cleaned -eq 1 ]; then
        print_status $YELLOW "  Waiting for graceful shutdown..."
        sleep 3
    fi
    
    # Force kill any remaining processes by port
    for port in "${API_PORTS[@]}" "${BLAZOR_PORTS[@]}"; do
        if is_port_in_use $port; then
            local pid=$(get_port_process $port)
            print_status $RED "  Force killing process on port $port (PID: $pid)"
            kill -9 $pid 2>/dev/null
            ((total_cleaned++))
        fi
    done
    
    # Kill by process name (catch any we missed)
    local api_pids=$(pgrep -f "AgentCouncil.API" 2>/dev/null)
    local blazor_pids=$(pgrep -f "AgentCouncil.BlazorWasm" 2>/dev/null)
    local dotnet_pids=$(pgrep -f "dotnet.*AgentCouncil" 2>/dev/null)
    
    if [ ! -z "$api_pids" ]; then
        print_status $YELLOW "  Killing API processes: $api_pids"
        echo $api_pids | xargs kill -9 2>/dev/null
        ((total_cleaned++))
    fi
    
    if [ ! -z "$blazor_pids" ]; then
        print_status $YELLOW "  Killing Blazor processes: $blazor_pids"
        echo $blazor_pids | xargs kill -9 2>/dev/null
        ((total_cleaned++))
    fi
    
    if [ ! -z "$dotnet_pids" ]; then
        print_status $YELLOW "  Killing dotnet processes: $dotnet_pids"
        echo $dotnet_pids | xargs kill -9 2>/dev/null
        ((total_cleaned++))
    fi
    
    # Final verification
    sleep 1
    local still_running=0
    for port in "${API_PORTS[@]}" "${BLAZOR_PORTS[@]}"; do
        if is_port_in_use $port; then
            print_status $RED "  ⚠️  Port $port is still in use"
            ((still_running++))
        fi
    done
    
    if [ $still_running -eq 0 ]; then
        print_status $GREEN "✅ All processes stopped successfully"
        if [ $total_cleaned -gt 0 ]; then
            print_status $GREEN "  Stopped $total_cleaned process(es)"
        else
            print_status $GREEN "  No processes were running"
        fi
    else
        print_status $RED "❌ Some processes may still be running"
        print_status $YELLOW "  You may need to manually kill them or restart your terminal"
    fi
}

# Function to check if .NET is available
check_dotnet() {
    if ! command -v dotnet &> /dev/null; then
        print_status $RED "❌ .NET is not installed or not in PATH"
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    print_status $GREEN "✅ .NET version: $dotnet_version"
}

# Function to build projects
build_projects() {
    print_status $BLUE "🔨 Building projects..."
    
    # Build API
    print_status $YELLOW "  Building API..."
    cd "$SCRIPT_DIR/AgentCouncil.API"
    if dotnet build --configuration Release --verbosity quiet > $STARTUP_LOG 2>&1; then
        print_status $GREEN "  ✅ API build successful"
    else
        print_status $RED "  ❌ API build failed"
        print_status $RED "  Check: cat $STARTUP_LOG"
        return 1
    fi
    
    # Build Blazor
    print_status $YELLOW "  Building Blazor..."
    cd "$SCRIPT_DIR/AgentCouncil.BlazorWasm"
    if dotnet build --configuration Release --verbosity quiet > $STARTUP_LOG 2>&1; then
        print_status $GREEN "  ✅ Blazor build successful"
    else
        print_status $RED "  ❌ Blazor build failed"
        print_status $RED "  Check: cat $STARTUP_LOG"
        return 1
    fi
    
    cd "$SCRIPT_DIR"
    print_status $GREEN "✅ All builds complete"
    return 0
}

# Function to start API
start_api() {
    print_status $BLUE "🚀 Starting API..."
    
    cd "$SCRIPT_DIR/AgentCouncil.API"
    nohup dotnet run --urls "https://localhost:7213;http://localhost:5068" > $API_LOG 2>&1 &
    local api_pid=$!
    cd "$SCRIPT_DIR"
    
    print_status $GREEN "  ✅ API process started (PID: $api_pid)"
    
    # Wait for API to be ready
    print_status $YELLOW "  Waiting for API to be ready..."
    local wait_count=0
    while [ $wait_count -lt $MAX_WAIT_TIME ]; do
        if curl -s -k -o /dev/null -w "%{http_code}" "$API_URL/openapi/v1.json" 2>/dev/null | grep -q "200"; then
            print_status $GREEN "  ✅ API is ready! (took ${wait_count}s)"
            return 0
        fi
        
        # Check if process is still running
        if ! kill -0 $api_pid 2>/dev/null; then
            print_status $RED "  ❌ API process died unexpectedly"
            print_status $RED "  Check logs: cat $API_LOG"
            return 1
        fi
        
        echo -n "."
        sleep 1
        ((wait_count++))
    done
    
    print_status $RED "  ❌ API failed to start within ${MAX_WAIT_TIME}s"
    print_status $RED "  Check logs: cat $API_LOG"
    return 1
}

# Function to start Blazor
start_blazor() {
    print_status $BLUE "🚀 Starting Blazor..."
    
    cd "$SCRIPT_DIR/AgentCouncil.BlazorWasm"
    nohup dotnet watch run --urls "https://localhost:7263;http://localhost:5033" > $BLAZOR_LOG 2>&1 &
    local blazor_pid=$!
    cd "$SCRIPT_DIR"
    
    print_status $GREEN "  ✅ Blazor process started (PID: $blazor_pid)"
    
    # Wait for Blazor to be ready
    print_status $YELLOW "  Waiting for Blazor to be ready..."
    local wait_count=0
    while [ $wait_count -lt $MAX_WAIT_TIME ]; do
        if curl -s -k -o /dev/null -w "%{http_code}" "$BLAZOR_URL" 2>/dev/null | grep -q "200"; then
            print_status $GREEN "  ✅ Blazor is ready! (took ${wait_count}s)"
            return 0
        fi
        
        # Check if process is still running
        if ! kill -0 $blazor_pid 2>/dev/null; then
            print_status $RED "  ❌ Blazor process died unexpectedly"
            print_status $RED "  Check logs: cat $BLAZOR_LOG"
            return 1
        fi
        
        echo -n "."
        sleep 1
        ((wait_count++))
    done
    
    print_status $RED "  ❌ Blazor failed to start within ${MAX_WAIT_TIME}s"
    print_status $RED "  Check logs: cat $BLAZOR_LOG"
    return 1
}

# Function to validate services
validate_services() {
    print_status $BLUE "🔍 Validating services..."
    
    # Check API
    if curl -s -k -o /dev/null -w "%{http_code}" "$API_URL/openapi/v1.json" 2>/dev/null | grep -q "200"; then
        print_status $GREEN "  ✅ API is responding"
    else
        print_status $RED "  ❌ API is not responding"
        return 1
    fi
    
    # Check Blazor
    if curl -s -k -o /dev/null -w "%{http_code}" "$BLAZOR_URL" 2>/dev/null | grep -q "200"; then
        print_status $GREEN "  ✅ Blazor is responding"
    else
        print_status $RED "  ❌ Blazor is not responding"
        return 1
    fi
    
    # Test API endpoint
    local test_response=$(curl -s -k -X POST "$API_URL/api/agents/chief_analyst/chat" \
        -H "Content-Type: application/json" \
        -d '{"message":"test"}' \
        -w "%{http_code}" 2>/dev/null)
    
    local http_code=$(echo "$test_response" | tail -c 4)
    if [ "$http_code" = "200" ] || [ "$http_code" = "400" ]; then
        print_status $GREEN "  ✅ API endpoints are working"
    else
        print_status $YELLOW "  ⚠️  API endpoints may have issues (HTTP: $http_code)"
    fi
    
    return 0
}

# Function to show running processes
show_status() {
    print_status $BLUE "📊 Current Status:"
    
    local running=0
    for port in "${API_PORTS[@]}"; do
        if is_port_in_use $port; then
            local pid=$(get_port_process $port)
            print_status $GREEN "  • API (port $port): Running (PID: $pid)"
            ((running++))
        else
            print_status $RED "  • API (port $port): Not running"
        fi
    done
    
    for port in "${BLAZOR_PORTS[@]}"; do
        if is_port_in_use $port; then
            local pid=$(get_port_process $port)
            print_status $GREEN "  • Blazor (port $port): Running (PID: $pid)"
            ((running++))
        else
            print_status $RED "  • Blazor (port $port): Not running"
        fi
    done
    
    if [ $running -eq 0 ]; then
        print_status $GREEN "✅ No Agent Council processes are running"
    else
        print_status $YELLOW "⚠️  $running process(es) are still running"
    fi
}

# Function to present final status
present_status() {
    print_header "🎉 Agent Council is Running!"
    
    print_status $GREEN "✅ All services are up and running"
    echo ""
    
    print_status $PURPLE "🌐 Quick Links:"
    echo "  • Blazor App:    $BLAZOR_URL"
    echo "  • API:           $API_URL"
    echo "  • API Docs:      $API_DOCS_URL"
    echo ""
    
    print_status $PURPLE "📊 Service Status:"
    for port in "${API_PORTS[@]}"; do
        if is_port_in_use $port; then
            local pid=$(get_port_process $port)
            print_status $GREEN "  • API (port $port): Running (PID: $pid)"
        else
            print_status $RED "  • API (port $port): Not running"
        fi
    done
    
    for port in "${BLAZOR_PORTS[@]}"; do
        if is_port_in_use $port; then
            local pid=$(get_port_process $port)
            print_status $GREEN "  • Blazor (port $port): Running (PID: $pid)"
        else
            print_status $RED "  • Blazor (port $port): Not running"
        fi
    done
    
    echo ""
    print_status $PURPLE "📋 Useful Commands:"
    echo "  • View API logs:    tail -f $API_LOG"
    echo "  • View Blazor logs: tail -f $BLAZOR_LOG"
    echo "  • Stop all:         ./agentcouncil.sh stop"
    echo "  • Restart:          ./agentcouncil.sh start"
    echo ""
    
    print_status $YELLOW "💡 Tip: Click the links above to open in your browser!"
    echo ""
}

# Function to test API
test_api() {
    print_status $CYAN "🧪 Running API tests..."
    echo ""
    
    # Test 1: Valid message
    print_status $BLUE "Test 1: Valid message"
    RESPONSE=$(curl -X POST "$API_URL/api/agents/chief_analyst/chat" \
      -H "Content-Type: application/json" \
      -d '{"message":"Hello from test!"}' \
      -k -s -w "\n%{http_code}")
    
    HTTP_CODE=$(echo "$RESPONSE" | tail -n 1)
    if [ "$HTTP_CODE" = "200" ]; then
        print_status $GREEN "✓ Status: 200 OK"
    else
        print_status $RED "✗ Status: $HTTP_CODE"
    fi
    
    # Test 2: Empty message
    print_status $BLUE "Test 2: Empty message validation"
    HTTP_CODE2=$(curl -X POST "$API_URL/api/agents/chief_analyst/chat" \
      -H "Content-Type: application/json" \
      -d '{"message":""}' \
      -k -s -w "%{http_code}" -o /dev/null)
    
    if [ "$HTTP_CODE2" = "400" ]; then
        print_status $GREEN "✓ Correctly rejected (400)"
    else
        print_status $RED "✗ Expected 400, got: $HTTP_CODE2"
    fi
    
    # Test 3: CORS
    print_status $BLUE "Test 3: CORS headers"
    CORS_HEADER=$(curl -X POST "$API_URL/api/agents/chief_analyst/chat" \
      -H "Content-Type: application/json" \
      -H "Origin: https://localhost:7263" \
      -d '{"message":"test"}' \
      -k -s -I | grep -i "access-control-allow-origin")
    
    if [ -n "$CORS_HEADER" ]; then
        print_status $GREEN "✓ CORS headers present"
    else
        print_status $RED "✗ CORS headers missing"
    fi
    
    echo ""
}

# Function to show help
show_help() {
    print_status $CYAN "Agent Council - Unified Management Script"
    echo ""
    echo "Usage: ./agentcouncil.sh [command]"
    echo ""
    echo "Commands:"
    echo "  ${GREEN}start${NC}     - Start both API and Blazor (default)"
    echo "  ${GREEN}api${NC}       - Start API only"
    echo "  ${GREEN}stop${NC}      - Stop all running instances"
    echo "  ${GREEN}restart${NC}   - Stop and start everything"
    echo "  ${GREEN}build${NC}     - Build projects only"
    echo "  ${GREEN}test${NC}      - Run integration tests"
    echo "  ${GREEN}status${NC}    - Show current status"
    echo "  ${GREEN}logs${NC}      - Show live logs"
    echo "  ${GREEN}clean${NC}     - Stop and clean logs"
    echo "  ${GREEN}help${NC}      - Show this help"
    echo ""
    echo "Examples:"
    echo "  ./agentcouncil.sh         # Start everything"
    echo "  ./agentcouncil.sh test    # Test and optionally start"
    echo "  ./agentcouncil.sh stop    # Stop everything"
    echo "  ./agentcouncil.sh logs    # Watch logs"
    echo ""
    echo "URLs:"
    echo "  API:        https://localhost:7213"
    echo "  API Docs:   https://localhost:7213/scalar/v1"
    echo "  Blazor:     https://localhost:7263"
    echo ""
    echo "Logs:"
    echo "  API:     tail -f $API_LOG"
    echo "  Blazor:  tail -f $BLAZOR_LOG"
}

# Function to show live logs
show_logs() {
    print_status $BLUE "📋 Live Logs (Press Ctrl+C to exit)"
    echo ""
    print_status $YELLOW "API Logs:"
    tail -f $API_LOG &
    local api_tail_pid=$!
    
    print_status $YELLOW "Blazor Logs:"
    tail -f $BLAZOR_LOG &
    local blazor_tail_pid=$!
    
    # Cleanup function for logs
    cleanup_logs() {
        kill $api_tail_pid $blazor_tail_pid 2>/dev/null
        exit 0
    }
    
    trap cleanup_logs INT
    wait
}

# Function to handle cleanup on exit
cleanup_on_exit() {
    print_status $YELLOW "🛑 Shutting down..."
    cleanup_all
    print_status $GREEN "✅ Shutdown complete"
    exit 0
}

# Main execution
main() {
    local command=${1:-start}
    
    case $command in
        start)
            print_header "🚀 Starting Agent Council"
            
            # Set up signal handlers
            trap cleanup_on_exit INT TERM
            
            # Check prerequisites
            print_status $BLUE "📋 Checking prerequisites..."
            check_dotnet
            
            # Cleanup
            cleanup_all
            
            # Build
            if ! build_projects; then
                print_status $RED "❌ Build failed"
                exit 1
            fi
            
            # Start API
            if ! start_api; then
                print_status $RED "❌ Failed to start API"
                exit 1
            fi
            
            # Start Blazor
            if ! start_blazor; then
                print_status $RED "❌ Failed to start Blazor"
                cleanup_all
                exit 1
            fi
            
            # Validate
            if ! validate_services; then
                print_status $RED "❌ Service validation failed"
                cleanup_all
                exit 1
            fi
            
            # Present status
            present_status
            
            # Keep script running and handle Ctrl+C
            print_status $YELLOW "Press Ctrl+C to stop all services"
            while true; do
                sleep 1
            done
            ;;
            
        api)
            print_header "🚀 Starting API Only"
            
            # Set up signal handlers
            trap cleanup_on_exit INT TERM
            
            # Check prerequisites
            check_dotnet
            
            # Cleanup
            cleanup_all
            
            # Build
            if ! build_projects; then
                print_status $RED "❌ Build failed"
                exit 1
            fi
            
            # Start API
            if ! start_api; then
                print_status $RED "❌ Failed to start API"
                exit 1
            fi
            
            print_status $GREEN "✅ API is running!"
            print_status $YELLOW "API:        $API_URL"
            print_status $YELLOW "API Docs:   $API_DOCS_URL"
            print_status $YELLOW "Logs:       tail -f $API_LOG"
            echo ""
            print_status $YELLOW "Press Ctrl+C to stop"
            while true; do
                sleep 1
            done
            ;;
            
        stop)
            print_header "🛑 Stopping Agent Council"
            cleanup_all
            ;;
            
        restart)
            print_header "🔄 Restarting Agent Council"
            cleanup_all
            echo ""
            exec "$0" start
            ;;
            
        build)
            print_header "🔨 Building Projects"
            check_dotnet
            if build_projects; then
                print_status $GREEN "✅ Build complete"
            else
                print_status $RED "❌ Build failed"
                exit 1
            fi
            ;;
            
        test)
            print_header "🧪 Integration Test"
            cleanup_all
            echo ""
            if start_api; then
                echo ""
                test_api
                print_status $GREEN "✅ Test Complete"
                echo ""
                print_status $YELLOW "What next?"
                echo "  1) Start Blazor and keep both running"
                echo "  2) Stop API and exit"
                echo "  3) Keep API running and exit"
                echo ""
                read -p "Choice [1-3]: " choice
                
                case $choice in
                    1)
                        echo ""
                        start_blazor
                        present_status
                        print_status $YELLOW "Press Ctrl+C to stop"
                        trap cleanup_on_exit INT
                        while true; do
                            sleep 1
                        done
                        ;;
                    2)
                        cleanup_all
                        ;;
                    3)
                        print_status $GREEN "API still running at $API_URL"
                        print_status $YELLOW "Run './agentcouncil.sh stop' to stop it"
                        ;;
                    *)
                        cleanup_all
                        ;;
                esac
            fi
            ;;
            
        status)
            print_header "📊 Status Check"
            show_status
            ;;
            
        logs)
            print_header "📋 Live Logs"
            show_logs
            ;;
            
        clean)
            print_header "🧹 Clean Stop with Log Cleanup"
            cleanup_all
            print_status $YELLOW "Cleaning log files..."
            rm -f $API_LOG $BLAZOR_LOG $STARTUP_LOG 2>/dev/null
            print_status $GREEN "✅ Log files cleaned"
            ;;
            
        help|--help|-h)
            show_help
            ;;
            
        *)
            print_status $RED "Unknown command: $command"
            echo ""
            show_help
            exit 1
            ;;
    esac
}

# Run main function
main "$@"
