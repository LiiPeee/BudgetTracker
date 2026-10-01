using System;
using System.Text.Json.Serialization;

namespace BudgetTracker.Core.Domain.Entities;

public class Account : BaseEntity
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; }

    [JsonIgnore]
    public string Password { get; set; } = null!;

    public decimal Balance { get; set; }
    public string Role { get; set; } = "User";

    [JsonIgnore]
    public string? RefreshToken { get; set; }

    [JsonIgnore]
    public DateTimeOffset? RefreshTokenExpiryTime { get; set; }

    public bool EmailVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }

    [JsonIgnore]
    public string? EmailVerificationToken { get; set; }

    [JsonIgnore]
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    [JsonIgnore]
    public long VerifyAttempts { get; set; } = 0;

    [JsonIgnore]
    public long LoginAttempts { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public List<Transactions> Transactions { get; set; } = new();
}


