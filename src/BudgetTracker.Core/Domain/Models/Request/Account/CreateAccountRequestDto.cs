using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Core.Domain.Models.Request.Account;

public class CreateAccountRequestDto
{
    [StringLength(50)]
    public required string FirstName { get; set; }
    [StringLength(50)]
    public required string LastName { get; set; }
    [StringLength(64)]
    [EmailAddress]
    public required string Email { get; set; }
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Senha deve ter entre 8 e 128 caracteres")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$", ErrorMessage = "Senha deve conter: letra minúscula, maiúscula, número e caractere especial")]
    public required string Password { get; set; }
}


