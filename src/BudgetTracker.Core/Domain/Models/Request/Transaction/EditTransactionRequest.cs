using BudgetTracker.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BudgetTracker.Core.Domain.Models.Request.Transaction
{
    public class EditTransactionRequest
    {
        [Required(ErrorMessage = "Amount is required")]
        [JsonPropertyName("amount")]
        [Range(0.01, 999999.99)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "TransactionName is required")]
        [StringLength(50)]
        [JsonPropertyName("transactionName")]
        public string TransactionName { get; set; }

        [Required(ErrorMessage = "CategoryName is required")]
        [JsonPropertyName("category")]
        public string CategoryName { get; set; }

        [JsonPropertyName("subCategory")]
        [StringLength(30)]
        public string SubCategoryName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("type")]
        public TypeTransactions TypeTransaction { get; set; }

        [JsonPropertyName("paid")]
        [Required(ErrorMessage = "Paid is required")]
        public bool Paid { get; set; }

        [Range(1, 420)]
        [JsonPropertyName("numberOfInstallment")]
        public int? NumberOfInstallment { get; set; }

        // Opcionais: quando ausentes, o valor atual é mantido.
        [StringLength(30)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        // Texto, e não o enum, para que um valor desconhecido seja ignorado em vez de recusar a edição.
        [JsonPropertyName("recurrence")]
        public string? RecurrenceName { get; set; }

        [StringLength(50)]
        [JsonPropertyName("contactName")]
        public string ContactName { get; set; } = null!;
    }
}
