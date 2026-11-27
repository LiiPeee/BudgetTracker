using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.WebApi.Models.Auth
{
    public class VerifyTokenRequest
    {
        public string? Id { get; set; }

        public string? Token { get; set; }
    }
}


