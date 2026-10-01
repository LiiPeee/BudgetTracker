namespace BudgetTracker.Core.Domain.Entities
{
    public class Stock : BaseEntity, IAccountOwned
    {
        public long AccountId { get; set; }

        public string Ticker { get; set; }

        public string? Description { get; set; }

        public string Title { get; set; }

        public bool IsStock { get; set; }

        public string? FixedIncomeType { get; set; }

        public decimal? CdiRate { get; set; }

        public DateTime? InvestmentDate { get; set; }

        public decimal PriceBuyed { get; set; }

        public decimal PriceMarket { get; set; }

        public string Avarage { get; set; }

        public decimal Quantity { get; set; }
    }
}
