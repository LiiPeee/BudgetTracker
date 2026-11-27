using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetTracker.Core.Domain.Dtos.Request.Transaction
{
    public class PaidTransactionRequest
    {
        [Required]
        [Range(1, long.MaxValue, ErrorMessage = "TransactionId inválido")]
        public long TransactionId { get; set; }

        [Required]
        public bool Paid { get; set; }
    }
}


