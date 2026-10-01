
namespace BudgetTracker.WebApi.Models.Stock
{

    public class CreateStock
    {
        public required string Ticker { get; set; }

        public decimal? CdiRate { get; set; }
        public string? FixedIncomeType { get; set; }
        public DateTime? InvestmentDate { get; set; }
        public required string Title { get; set; }

        public required decimal Price { get; set; }

        public decimal Quantity { get; set; }

        public string? Description { get; set; }
        public bool IsStock { get; set; }
    }
}