# DealerPerformanceAgent Configuration

**Agent Type**: Dealer Performance Analyst  
**Purpose**: Analyze dealer network performance, rankings, and regional comparisons  
**Created**: November 2025  
**Status**: Active

---

## Agent Settings

| Setting | Value |
|---------|-------|
| **Model** | GPT-4-1 |
| **Temperature** | 0.3 |
| **Knowledge Source** | AgentCouncilFabricDataAgent |
| **Fabric Endpoint** | `<Your Fabric Data Agent endpoint URL>` |
| **Agent Description** | Dealer performance analyst specializing in dealer rankings, regional comparisons, and dealer network profitability analysis. Queries verified dealer metrics for network optimization insights. |

---

## System Instructions

```markdown
# Role
You are a Dealer Performance Analyst specializing in automotive dealer network analysis and performance optimization.

# Scope
Analyze dealer performance metrics including:
- Dealer rankings by revenue, profit, units sold
- Regional dealer comparisons and benchmarking
- Dealer tier analysis (Tier 1, Tier 2, Tier 3)
- Quarter-over-quarter and month-over-month dealer trends
- Dealer-level margin, discount patterns, and profitability
- Top and bottom performer identification
- Regional dealer network health

# MANDATORY KNOWLEDGE RETRIEVAL
You MUST query the AgentCouncilFabricDataAgent BEFORE responding to any question.
NEVER fabricate dealer names, dealer performance numbers, or regional dealer data.

Before responding, verify:
✓ Did I query AgentCouncilFabricDataAgent?
✓ Are dealer names and numbers from actual data response?
✓ Am I staying within my scope (dealer performance only)?

# Key Business Context
- **Dealer Network**: 120 dealers across 8 regions
- **Dealer Tiers**: Tier 1 (high volume), Tier 2 (medium volume), Tier 3 (smaller dealers)
- **Campaign Period**: June-September 2025 (Azure Motors EV SUV promotional campaign)
- **Baseline Period**: March-May 2025 (pre-campaign comparison)
- **Logistics Disruption**: August-September 2025 affected West and Pacific NW regions (30% supply reduction)
- **Data Coverage**: July 2024 - December 2025 (18 months)
- **Regions**: 8 regions - West, Pacific NW, Northeast, Southeast, Midwest, Southwest, Mountain, South

# Response Format
Structure responses as:
1. **Key Finding**: Direct answer with specific dealer metrics
2. **Dealer Performance**: Specific dealer names, rankings, and numbers
3. **Regional Context**: How region affects dealer performance
4. **Comparative Insights**: vs other dealers, vs regional average, vs prior period
5. **Recommendation**: Actionable next steps for dealer network optimization

# Query Guidelines
- Use dealer_performance_monthly for aggregated dealer KPIs (total_revenue, total_profit, total_units, avg_margin_pct, avg_discount, stockout_risk_score)
- Join with dealers table to get dealer_name, region, tier
- Use car_sales for transaction-level dealer analysis when needed
- Month format: 'YYYY-MM' (e.g., '2025-06')
- Q3 2025 months: ('2025-07','2025-08','2025-09')
- Q2 2025 months: ('2025-04','2025-05','2025-06')
- Campaign months: ('2025-06','2025-07','2025-08','2025-09')
- Use TOP N for rankings (not LIMIT)

# Out of Scope - Politely Decline
When asked about topics outside dealer performance, respond:

"That question is outside my dealer performance expertise. For [TOPIC], please consult:
- Product performance, revenue by brand/model, campaign effectiveness → Sales Insights specialist
- Inventory levels, stockouts, supply chain issues → Inventory Operations specialist
- Individual customer information or behavior → Not available
- Financial forecasting beyond available data → Not available"

Then ask: "Can I help you with any dealer performance analysis instead?"

# Critical Rules
- NEVER invent dealer names or performance numbers
- NEVER answer questions about product sales performance, inventory, or supply chain
- NEVER provide data outside July 2024 - December 2025 timeframe
- ALWAYS query the Fabric Data Agent before generating responses
- ALWAYS include dealer names when ranking or comparing dealers
- ALWAYS cite specific time periods in your analysis
- ALWAYS mention region context for dealer performance
```

---

## Data Schema Reference

### Primary Table: dealer_performance_monthly
**Columns**: dealer_id, region, month, total_units, total_revenue, total_profit, avg_discount, avg_margin_pct, stockout_risk_score

**Key Features**:
- Monthly aggregated dealer KPIs
- Pre-calculated metrics for faster queries
- Includes stockout risk scoring

### Dimension Table: dealers
**Columns**: dealer_id, dealer_name, region, tier

**Key Features**:
- Dealer identifying information
- Tier classification (1, 2, 3)
- Regional assignment

### Supporting Tables
- **car_sales**: Transaction-level detail (can be used for dealer-specific deep dives)
- **inventory**: Dealer-level inventory positions (out of scope - refer to Inventory Ops)
- **models**: Product information (out of scope for dealer analysis)

---

## Test Prompts

### ✅ Test 1: Top Dealers by Revenue
**Prompt:**
```
Which top 10 dealers had the highest revenue during Q3 2025?
```

**Expected Behavior:**
- Calls Fabric Data Agent
- Returns 10 dealer names with revenue numbers
- Uses dealer_performance_monthly joined with dealers table
- Filters months ('2025-07','2025-08','2025-09')
- Sorted by revenue descending

**Validates**: Basic dealer ranking, table joins, TOP N syntax

---

### ✅ Test 2: Regional Dealer Comparison
**Prompt:**
```
Compare average dealer profit between West and Pacific NW regions during Q3 2025
```

**Expected Behavior:**
- Shows average profit per dealer for both regions
- Groups by region
- Mentions logistics disruption context (Aug-Sep)
- Provides insight on regional differences

**Validates**: Regional aggregation, business context awareness, comparison logic

---

### ✅ Test 3: Bottom Performers
**Prompt:**
```
Which dealers had the lowest profit in September 2025?
```

**Expected Behavior:**
- Returns dealer names with profit numbers
- Shows bottom performers (ascending order or TOP N with negative sort)
- Single month filter (month = '2025-09')
- Includes region for context

**Validates**: Bottom ranking logic, dealer identification, single period filtering

---

### ✅ Test 4: Tier Analysis
**Prompt:**
```
How did Tier 1 dealers perform compared to Tier 2 and Tier 3 dealers during the campaign period?
```

**Expected Behavior:**
- Groups by tier (from dealers table)
- Aggregates metrics (revenue, profit, units) by tier
- Campaign period filter (Jun-Sep 2025)
- Comparative insights across tiers

**Validates**: Tier segmentation, multi-tier comparison, campaign period filtering

---

### ✅ Test 5: Dealer Margin Analysis
**Prompt:**
```
Which dealers had the highest average margin percentage in Q3 2025?
```

**Expected Behavior:**
- Returns dealer names with avg_margin_pct
- Uses dealer_performance_monthly
- Q3 2025 filter
- Sorted by margin descending

**Validates**: Profitability metrics, dealer-specific analysis

---

### ✅ Test 6: Trend Analysis
**Prompt:**
```
Show me the top 5 dealers' revenue trend from June through September 2025
```

**Expected Behavior:**
- Returns 5 dealers with monthly revenue breakdown
- 4 months of data (Jun, Jul, Aug, Sep)
- Shows progression/trend for each dealer
- Identifies consistent vs volatile performers

**Validates**: Time series analysis, multi-month tracking, dealer-level trends

---

### ✅ Test 7: Regional Top Performer
**Prompt:**
```
Who was the top performing dealer in the Northeast region during the campaign period?
```

**Expected Behavior:**
- Single dealer name and metrics
- Region filter (Northeast)
- Campaign period aggregation (Jun-Sep 2025)
- Explains why they were top performer

**Validates**: Regional filtering, single dealer identification, performance criteria

---

### ✅ Test 8: Out of Scope - Product Performance
**Prompt:**
```
Which models sold best during the campaign?
```

**Expected Behavior:**
- ✅ Politely declines
- ✅ Refers to "Sales Insights specialist"
- ✅ Offers alternative dealer analysis
- ✅ Does NOT answer with model performance data

**Validates**: Scope boundaries, proper referral, Task Adherence

---

### ✅ Test 9: Out of Scope - Inventory Question
**Prompt:**
```
Which dealers had the most stockouts in August 2025?
```

**Expected Behavior:**
- ✅ Politely declines OR provides stockout_risk_score from dealer_performance_monthly
- ✅ If using risk score: stays within dealer performance scope
- ✅ For detailed stockout incidents: refers to "Inventory Operations specialist"

**Validates**: Scope boundaries, understanding of available vs out-of-scope metrics

---

### ✅ Test 10: Quarter-over-Quarter Comparison
**Prompt:**
```
Compare top 10 dealers' performance in Q3 2025 versus Q2 2025
```

**Expected Behavior:**
- Returns 10 dealers with Q2 and Q3 metrics
- Shows change/growth percentages
- Both quarters filtered correctly (Q2: Apr-Jun, Q3: Jul-Sep)
- Identifies improving vs declining dealers

**Validates**: Multi-period comparison, growth calculations, dealer rankings across time

---

## Validation Checklist

When testing each prompt, verify:

### Response Quality
- [ ] Provides specific dealer names (not generic "dealers")
- [ ] Includes actual performance numbers
- [ ] Cites time periods explicitly
- [ ] Follows 5-part response format
- [ ] Includes regional context
- [ ] Provides actionable recommendations

### Technical Correctness
- [ ] Calls Fabric Data Agent (check Trace tab)
- [ ] Uses correct tables (dealer_performance_monthly + dealers join)
- [ ] References actual columns (total_revenue, total_profit, avg_margin_pct)
- [ ] Applies correct filters (region, month, tier)
- [ ] Uses TOP N syntax (not LIMIT)
- [ ] Returns dealer_name from dealers table

### Task Adherence
- [ ] Stays within dealer performance scope
- [ ] Declines product/inventory questions appropriately
- [ ] Does not fabricate dealer names or data
- [ ] Queries knowledge source before responding
- [ ] References specialists for other domains

### Business Context
- [ ] Acknowledges dealer tiers when relevant
- [ ] Recognizes logistics disruption impact (Aug-Sep West/Pacific NW)
- [ ] Understands campaign period context
- [ ] Applies correct regional assignments

---

## Expected AI Quality Metrics

After implementing MANDATORY KNOWLEDGE RETRIEVAL instructions:

| Metric | Target | Notes |
|--------|--------|-------|
| **Task Adherence** | 4-5/5 | Should stay in dealer scope, query data before responding |
| **Groundedness** | 4-5/5 | All dealer names and numbers from Fabric Data Agent |
| **Relevance** | 4-5/5 | Answers dealer questions directly |
| **Coherence** | 4-5/5 | Structured 5-part format with regional context |

---

## Troubleshooting

### Issue: Agent not returning dealer names
**Solution**: Ensure JOIN with dealers table is working, verify dealer_name column is in SELECT statement

### Issue: Wrong aggregation level
**Solution**: Use dealer_performance_monthly for monthly KPIs, not car_sales unless transaction-level detail needed

### Issue: Agent answering product performance questions
**Solution**: Strengthen "Out of Scope - Politely Decline" section, emphasize dealer-only focus

### Issue: Missing regional context
**Solution**: Always include region in GROUP BY or display, join dealers table for region column

---

## Integration Notes

**Multi-Agent Architecture**: This agent is designed to work as a specialist in a hub-and-spoke model:
- **Chief Analyst Agent** routes dealer-related questions to this agent
- This agent focuses ONLY on dealer performance and rankings
- Refers product/sales questions to **SalesInsightsAgent**
- Refers inventory questions to **InventoryOpsAgent**

**API Deployment**: After validation, deploy this agent to get an API endpoint URL for Chief Analyst integration

---

## Sample Queries Expected from Chief Analyst

When Chief Analyst routes questions, expect patterns like:
- "Which dealers performed best in [region] during [period]?"
- "Compare dealer performance across [regions]"
- "Show me bottom performing dealers in [period]"
- "How did [tier] dealers perform during campaign?"
- "Dealer trends from [start month] to [end month]"

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Nov 2025 | Initial creation with dealer performance focus |

---

## Related Files

- **SalesInsightsAgent Configuration**: `SalesInsightsAgent_Configuration.md`
- **Example Queries**: `examples_fabric_upload.json`
- **Data Generator**: `Ignite2025_Demo_Data_Generator.ipynb`

---

**Last Updated**: November 12, 2025  
**Maintained By**: Agent Council Project Team
