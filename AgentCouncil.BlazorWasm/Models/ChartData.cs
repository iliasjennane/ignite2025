using System.Text.Json.Serialization;

namespace AgentCouncil.BlazorWasm.Models;

public class ChartResponse
{
    [JsonPropertyName("metric")]
    public string Metric { get; set; } = "";

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("values")]
    public List<object> Values { get; set; } = new();
}

public class BrandSalesData
{
    [JsonPropertyName("brand")]
    public string Brand { get; set; } = "";

    [JsonPropertyName("total_units_sold")]
    public int TotalUnitsSold { get; set; }

    [JsonPropertyName("total_revenue_m")]
    public double TotalRevenueM { get; set; }

    [JsonPropertyName("avg_profit_margin_pct")]
    public double AvgProfitMarginPct { get; set; }

    [JsonPropertyName("margin_drop_gt_5pct")]
    public bool MarginDropGt5Pct { get; set; }
}

public class CustomerData
{
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("lifetime_spend_m")]
    public double LifetimeSpendM { get; set; }

    [JsonPropertyName("avg_annual_income_band")]
    public string AvgAnnualIncomeBand { get; set; } = "";

    [JsonPropertyName("repeat_purchases")]
    public int RepeatPurchases { get; set; }
}

public class DealerGrowthData
{
    [JsonPropertyName("dealer_name")]
    public string DealerName { get; set; } = "";

    [JsonPropertyName("units_sold_increase")]
    public int UnitsSoldIncrease { get; set; }

    [JsonPropertyName("contributing_brands")]
    public List<string> ContributingBrands { get; set; } = new();

    [JsonPropertyName("regions")]
    public List<string> Regions { get; set; } = new();
}
