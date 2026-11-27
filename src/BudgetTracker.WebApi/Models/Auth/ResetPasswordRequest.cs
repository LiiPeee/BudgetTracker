using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.WebApi.Models.Auth
{
    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string email  { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Senha deve ter entre 8 e 128 caracteres")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$", ErrorMessage = "Senha deve conter: letra minúscula, maiúscula, número e caractere especial")]
        public string newPassword { get; set; }

        // Reset code sent to the user's email — required to authorize the password change.
        [Required]
        public string token { get; set; }
    }
}
