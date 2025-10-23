#!/bin/bash

# Fix all agent files except SalesInsights and OpsRisk which are already fixed
for file in Pages/Agents.DocEvidence.razor Pages/Agents.MarketContext.razor Pages/Agents.DataGateway.razor Pages/Agents.ChiefAnalyst.razor; do
    if [ -f "$file" ]; then
        echo "Fixing $file..."
        
        # Replace complex style expressions with method calls
        sed -i '' 's/Style="max-width: 80%; border-radius: 12px; @(message\.IsUser ? "background: linear-gradient([^"]*); color: white;" : "background: #f8f9fa; color: #333;")"/Style="@GetMessageStyle(message)"/g' "$file"
        
        # Replace timestamp style expressions
        sed -i '' 's/Style="@(message\.IsUser ? "color: rgba([^"]*);" : "color: #999;") margin-top: 4px;"/Style="@GetTimestampStyle(message)"/g' "$file"
        
        # Add helper methods before the closing @code tag
        sed -i '' '/^}$/i\
    private string GetMessageStyle(ChatMessage message)\
    {\
        var baseStyle = "max-width: 80%; border-radius: 12px;";\
        if (message.IsUser)\
        {\
            return $"{baseStyle} background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;";\
        }\
        else\
        {\
            return $"{baseStyle} background: #f8f9fa; color: #333;";\
        }\
    }\
\
    private string GetTimestampStyle(ChatMessage message)\
    {\
        if (message.IsUser)\
        {\
            return "color: rgba(255,255,255,0.7); margin-top: 4px;";\
        }\
        else\
        {\
            return "color: #999; margin-top: 4px;";\
        }\
    }\
' "$file"
    fi
done

echo "Done fixing agent files!"
