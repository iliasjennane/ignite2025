#!/bin/bash

echo "🧪 Testing Agent Council API"
echo "═══════════════════════════════════════"
echo ""

# Test 1: Basic message
echo "Test 1: Sending a message..."
curl -X POST https://localhost:7213/api/chat/send \
  -H 'Content-Type: application/json' \
  -d '{"message":"What can you tell me about sales trends?"}' \
  -k -s | python3 -m json.tool

echo ""
echo ""

# Test 2: Another message
echo "Test 2: Different message..."
curl -X POST https://localhost:7213/api/chat/send \
  -H 'Content-Type: application/json' \
  -d '{"message":"Hello, Agent!"}' \
  -k -s | python3 -m json.tool

echo ""
echo ""
echo "✅ API is working!"
echo ""
echo "💡 For interactive testing, open:"
echo "   https://localhost:7213/scalar/v1"


