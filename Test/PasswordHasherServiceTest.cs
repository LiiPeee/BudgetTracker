using BudgetTracker.Application.Service;
using Microsoft.AspNet.Identity;

namespace Test;

[TestFixture]
public class PasswordHasherServiceTest
{
    private const string ValidPassword = "ValidPass1!";

    private PasswordHasherService _service;

    [SetUp]
    public void Setup() => _service = new PasswordHasherService();

    [Test]
    public void Hash_ProducesCurrentFormatHash_ThatVerifiesWithoutRehash()
    {
        var hash = _service.Hash(ValidPassword);

        Assert.That(hash, Does.StartWith("AQAAAA")); // versão 3 (PBKDF2-HMACSHA512/100k)
        Assert.That(_service.Verify(hash, ValidPassword, out var rehashNeeded), Is.True);
        Assert.That(rehashNeeded, Is.False);
    }

    [Test]
    public void Verify_LegacyIdentity2xHash_ReturnsTrueAndSignalsRehash()
    {
        var legacyHash = new PasswordHasher().HashPassword(ValidPassword);

        Assert.That(_service.Verify(legacyHash, ValidPassword, out var rehashNeeded), Is.True);
        Assert.That(rehashNeeded, Is.True);
    }

    [Test]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _service.Hash(ValidPassword);

        Assert.That(_service.Verify(hash, "WrongPass1!", out _), Is.False);
    }

    [Test]
    public void Verify_MalformedHash_ReturnsFalseWithoutThrowing()
    {
        Assert.That(_service.Verify("not-a-valid-hash", ValidPassword, out _), Is.False);
        Assert.That(_service.Verify("", ValidPassword, out _), Is.False);
    }
}
