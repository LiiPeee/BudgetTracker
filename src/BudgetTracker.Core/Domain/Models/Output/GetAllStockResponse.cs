namespace BudgetTracker.Core.Domain.Models.Output;

public class GetAllStockResponse
{
    public string Ticker { get; set; }

    public decimal PriceMarket { get; set; }

    public decimal PriceBuyed { get; set; }

    public DateTime? InvestmentDate { get; set; }

    public decimal? CdiRate { get; set; }

    public decimal Quantity { get; set; }

    public string Percentage { get; set; }

    public bool IsStock { get; set; }

    public string? FixedIncomeType { get; set; }
}