#!/bin/bash
# Setup script for CopilotKit UI

echo "🚀 Setting up Agent Council CopilotKit UI..."
echo ""

# Create project directory
mkdir -p AgentCouncil.CopilotUI
cd AgentCouncil.CopilotUI

# Check if already initialized
if [ -f "package.json" ]; then
    echo "⚠️  package.json exists - skipping Next.js init"
    echo "   If you want to reinstall, delete the AgentCouncil.CopilotUI folder first"
else
    echo "📦 Initializing Next.js project..."
    echo "   This will take a minute..."
    npx create-next-app@latest . --typescript --tailwind --app --no-src-dir --use-npm --import-alias "@/*"
fi

# Install CopilotKit dependencies
echo ""
echo "📚 Installing CopilotKit packages..."
npm install @copilotkit/react-core@latest @copilotkit/react-ui@latest @copilotkit/runtime@latest

# Create .env.local
echo ""
echo "⚙️  Creating .env.local..."
cat > .env.local << 'EOF'
NEXT_PUBLIC_API_URL=https://localhost:7213
NODE_TLS_REJECT_UNAUTHORIZED=0
EOF

echo ""
echo "✅ Setup complete!"
echo ""
echo "📝 Next steps:"
echo "1. Files will be created automatically"
echo "2. Run: ./start.sh"
echo "3. Open: http://localhost:3000"
