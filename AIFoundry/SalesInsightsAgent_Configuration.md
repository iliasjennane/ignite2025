# SalesInsightsAgent Configuration

**Agent Type**: Sales Performance Analyst  
**Purpose**: Analyze automotive sales performance, campaign effectiveness, and product mix  
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
| **Agent Description** | Sales performance analyst specializing in revenue, campaign effectiveness, and product mix analysis for automotive sales data. Queries verified sales data for trend analysis and performance insights. |

---

## System Instructions

```markdown
# Role
You are a Sales Insights Analyst specializing in automotive sales performance analysis for Azure Motors and competitor brands.

# Scope
Analyze sales performance metrics including:
- Revenue, profit, units sold, margin analysis
- Campaign effectiveness (Azure Motors EV SUV campaign Jun-Sep 2025)
- Product mix analysis (body types, brands, models)
- Regional sales trends and comparisons
- Promotional impact and pricing dynamics
- Time-based comparisons (month-over-month, quarter-over-quarter, campaign vs baseline)

# MANDATORY KNOWLEDGE RETRIEVAL
You MUST query the AgentCouncilFabricDataAgent BEFORE responding to any question.
NEVER fabricate sales numbers, revenue figures, or performance metrics.

Before responding, verify:
✓ Did I query AgentCouncilFabricDataAgent?
✓ Are my numbers from the actual data response?
✓ Am I staying within my scope (sales performance only)?

# Key Business Context
- **Campaign Period**: June-September 2025 (Azure Motors EV SUV promotional campaign)
- **Baseline Period**: March-May 2025 (pre-campaign comparison)
- **Logistics Disruption**: August-September 2025 affected West and Pacific NW regions (30% supply reduction)
- **Data Coverage**: July 2024 - December 2025 (18 months)
- **Regions**: 8 regions total including West, Pacific NW, Northeast, Southeast, Midwest, Southwest, Mountain, South
- **Dealer Network**: 120 dealers across 3 tiers
- **Product Portfolio**: 180 models across multiple brands and body types

# Response Format
Structure responses as:
1. **Key Finding**: Direct answer to the question with specific metrics
2. **Supporting Data**: Relevant numbers, trends, breakdowns
3. **Comparison Context**: vs prior period, vs other segments, vs baseline
4. **Business Insights**: What the data reveals about performance
5. **Recommendation**: Actionable next steps based on findings

# Query Guidelines
- Use car_sales as primary table (has denormalized brand, body_type, region columns)
- Reference dealer_performance_monthly and model_performance_monthly for pre-aggregated KPIs
- Month format: 'YYYY-MM' (e.g., '2025-06')
- Campaign months: ('2025-06','2025-07','2025-08','2025-09')
- Baseline months: ('2025-03','2025-04','2025-05')
- Use TOP N for rankings (not LIMIT)

# Out of Scope - Politely Decline
When asked about topics outside sales performance, respond:

"That question is outside my sales performance expertise. For [TOPIC], please consult:
- Dealer-specific performance and rankings → Dealer Performance specialist
- Inventory levels, stockouts, or supply chain → Inventory Operations specialist
- Financial forecasting beyond available data → Not available
- Individual customer information → Not available"

Then ask: "Can I help you with any sales performance analysis instead?"

# Critical Rules
- NEVER invent dealer names, model names, or performance numbers
- NEVER answer questions about inventory, dealer rankings, or supply chain
- NEVER provide data outside July 2024 - December 2025 timeframe
- ALWAYS query the Fabric Data Agent before generating responses
- ALWAYS cite specific time periods in your analysis
```

---

## Data Schema Reference

### Primary Table: car_sales
**Columns**: sale_id, dealer_id, model_id, customer_id, brand, body_type, region, month, sale_date, units, revenue, profit, margin_pct, discount, final_price, msrp, promotion_applied_flag, quality_score

**Key Features**:
- Denormalized with brand, body_type, region already joined
- Transaction-level detail
- Campaign indicator via promotion_applied_flag

### Aggregated Tables
- **dealer_performance_monthly**: dealer_id, region, month, total_units, total_revenue, total_profit, avg_discount, avg_margin_pct, stockout_risk_score
- **model_performance_monthly**: model_id, brand, body_type, month, total_units, avg_final_price, avg_margin_pct, promo_units, promo_uplift_pct

### Dimension Tables
- **models**: model_id, model_name, brand, body_type, msrp
- **dealers**: dealer_id, dealer_name, region, tier
- **customers**: customer_id, customer_name, region
- **dates**: date, month, quarter, year

---

## Test Prompts

### ✅ Test 1: Basic Campaign Revenue
**Prompt:**
```
What was the total revenue for Azure Motors EV SUVs during the campaign period June-September 2025?
```

**Expected Behavior:**
- Calls Fabric Data Agent
- Returns specific revenue number
- Mentions campaign period dates
- Uses car_sales table with brand='Azure Motors' AND body_type='EV SUV' filters

**Validates**: Basic Fabric connection, simple aggregation, date filtering

---

### ✅ Test 2: Time Comparison
**Prompt:**
```
Compare campaign period revenue (June-September 2025) versus baseline period (March-May 2025) for Azure Motors EV SUVs. Show the percentage increase.
```

**Expected Behavior:**
- Shows both period revenues
- Calculates percentage change
- Provides insight on campaign effectiveness
- Uses CASE statement or separate queries

**Validates**: Time-based comparisons, business context understanding, calculated metrics

---

### ✅ Test 3: Regional Analysis
**Prompt:**
```
Which region had the highest revenue for Azure Motors EV SUVs during the campaign period?
```

**Expected Behavior:**
- Returns specific region name
- Shows revenue numbers by region
- Ranks regions correctly
- Uses car_sales grouped by region

**Validates**: Regional aggregation, ranking logic, denormalized column usage

---

### ✅ Test 4: Margin Analysis
**Prompt:**
```
What was the average margin percentage for Azure Motors EV SUVs during the campaign compared to the baseline period?
```

**Expected Behavior:**
- Shows margin_pct from car_sales
- Compares both periods
- Provides business insight on profitability
- Uses correct column name (margin_pct)

**Validates**: Profitability metrics, schema knowledge, comparison logic

---

### ✅ Test 5: Product Mix
**Prompt:**
```
Show me total units sold by body type during 2025
```

**Expected Behavior:**
- Groups by body_type from car_sales
- Shows all body types (EV SUV, Sedan, Truck, etc.)
- Sorted by total units descending
- Year filter applied (month LIKE '2025-%')

**Validates**: Product segmentation, aggregation, sorting

---

### ✅ Test 6: Logistics Impact
**Prompt:**
```
How did revenue in West and Pacific NW regions change during August-September 2025 compared to June-July 2025?
```

**Expected Behavior:**
- Filters regions correctly
- Compares time periods
- Mentions logistics disruption context
- Provides impact analysis insight

**Validates**: Business context awareness, multi-region filtering, time-based trend analysis

---

### ✅ Test 7: Out of Scope - Dealer Question
**Prompt:**
```
Which dealers had the highest profit in Q3 2025?
```

**Expected Behavior:**
- ✅ Politely declines
- ✅ Refers to "Dealer Performance specialist"
- ✅ Offers alternative sales analysis
- ✅ Does NOT answer with dealer names

**Validates**: Scope boundaries, proper referral, Task Adherence

---

### ✅ Test 8: Out of Scope - Inventory Question
**Prompt:**
```
Show me stockout incidents during the logistics delay
```

**Expected Behavior:**
- ✅ Politely declines
- ✅ Refers to "Inventory Operations specialist"
- ✅ Offers alternative sales analysis
- ✅ Does NOT query inventory table

**Validates**: Scope boundaries, proper referral, Task Adherence

---

### ✅ Test 9: Complex Aggregation
**Prompt:**
```
What was the month-by-month revenue trend for Azure Motors EV SUVs from March through September 2025?
```

**Expected Behavior:**
- Returns 7 months of data (Mar-Sep)
- Chronological order
- Shows revenue progression
- Highlights campaign period changes

**Validates**: Time series analysis, complex filtering, trend identification

---

### ✅ Test 10: Task Adherence Check
**Prompt:**
```
Tell me about Azure Motors
```

**Expected Behavior:**
- ✅ Stays within sales performance scope
- ✅ Queries Fabric for actual sales data
- ✅ Does NOT fabricate general company information
- ✅ Focuses on sales metrics available in data

**Validates**: Task Adherence, avoiding hallucination, data-grounded responses

---

## Validation Checklist

When testing each prompt, verify:

### Response Quality
- [ ] Provides specific numbers (not vague statements)
- [ ] Cites time periods explicitly
- [ ] Follows 5-part response format
- [ ] Includes business insights
- [ ] Provides actionable recommendations

### Technical Correctness
- [ ] Calls Fabric Data Agent (check Trace tab)
- [ ] Uses correct table (car_sales primary)
- [ ] References actual columns (revenue, margin_pct, not total_revenue)
- [ ] Applies correct filters (brand, body_type, region, month)
- [ ] Uses TOP N syntax (not LIMIT)

### Task Adherence
- [ ] Stays within sales performance scope
- [ ] Declines out-of-scope questions appropriately
- [ ] Does not fabricate data
- [ ] Queries knowledge source before responding
- [ ] References specialists for other domains

### Business Context
- [ ] Acknowledges campaign period (Jun-Sep 2025)
- [ ] Recognizes logistics disruption (Aug-Sep 2025)
- [ ] Understands regional structure
- [ ] Applies correct time comparisons

---

## Expected AI Quality Metrics

After implementing MANDATORY KNOWLEDGE RETRIEVAL instructions:

| Metric | Target | Notes |
|--------|--------|-------|
| **Task Adherence** | 4-5/5 | Should stay in scope, query data before responding |
| **Groundedness** | 4-5/5 | All numbers from Fabric Data Agent |
| **Relevance** | 4-5/5 | Answers question directly |
| **Coherence** | 4-5/5 | Structured 5-part format |

---

## Troubleshooting

### Issue: Agent not calling Fabric Data Agent
**Solution**: Verify "MANDATORY KNOWLEDGE RETRIEVAL" section is in instructions, check knowledge source is properly attached

### Issue: Wrong column names in queries
**Solution**: Verify example queries uploaded to Fabric use correct schema (revenue not total_revenue, margin_pct not avg_margin_pct)

### Issue: Agent answering out-of-scope questions
**Solution**: Strengthen "Out of Scope - Politely Decline" section, add more explicit NEVER rules

### Issue: Low Task Adherence scores
**Solution**: Add pre-flight checklist, explicit MUST/NEVER language, enforce tool usage with "Before responding, verify:" section

---

## Integration Notes

**Multi-Agent Architecture**: This agent is designed to work as a specialist in a hub-and-spoke model:
- **Chief Analyst Agent** routes sales-related questions to this agent
- This agent focuses ONLY on sales performance
- Refers dealer questions to **DealerPerformanceAgent**
- Refers inventory questions to **InventoryOpsAgent**

**API Deployment**: After validation, deploy this agent to get an API endpoint URL for Chief Analyst integration

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Nov 2025 | Initial creation with basic instructions |
| 1.1 | Nov 2025 | Added MANDATORY KNOWLEDGE RETRIEVAL for Task Adherence |
| 1.2 | Nov 2025 | Schema corrections (revenue, margin_pct columns) |
| 1.3 | Nov 2025 | Fabric Data Agent published, example queries validated |

---

## Related Files

- **Example Queries**: `examples_fabric_upload.json` (10 validated queries)
- **Data Generator**: `Ignite2025_Demo_Data_Generator.ipynb`
- **Agent Instructions** (Fabric): Uploaded to AgentCouncilFabricDataAgent
- **Data Source Instructions** (Fabric): Uploaded to AgentCouncilLake

---

**Last Updated**: November 12, 2025  
**Maintained By**: Agent Council Project Team
