using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetTracker.Core.Domain.Models.Request.Stock
{
    public class CreateStockRequest
    {
        [StringLength(10, MinimumLength = 2, ErrorMessage = "NOme da ação deve ser menor que 10 e maior que 2")]
        public required string Ticker { get; set; }
        [StringLength(30, MinimumLength = 5, ErrorMessage = "Nome da ação deve ser maior que 5 e menor que 30")]
        public required string Title { get; set; }

        public decimal? CdiRate { get; set; }

        public DateTime? InvestmentDate { get; set; }

        public required decimal Price { get; set; }

        public string? FixedIncomeType { get; set; }

        public required decimal Quantity { get; set; }

        public string? Description { get; set; }

        public bool IsStock { get; set; }
    }
}
