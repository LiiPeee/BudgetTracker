using BudgetTracker.Core.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Test;

[TestFixture]
public class GlobalExceptionMiddlewareTest
{
    private static async Task<(int StatusCode, string Body)> InvokeWithAsync(Func<Task> next)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();

        var middleware = new GlobalExceptionMiddleware(_ => next(), NullLogger<GlobalExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static string ExtractMessage(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";
    }

    [Test]
    public async Task InfrastructureException_DoesNotLeakRawMessage()
    {
        var (status, body) = await InvokeWithAsync(() => throw new InvalidOperationException("Failed to retrieve inserted record from Transactions."));

        Assert.That(status, Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(ExtractMessage(body), Is.EqualTo("An error occurred while processing your request"));
        Assert.That(body, Does.Not.Contain("Failed to retrieve inserted record"));
    }

    [Test]
    public async Task DomainException_ExposesItsSafeMessageAndStatus()
    {
        var (status, body) = await InvokeWithAsync(() => throw new DomainException("limit amount must be greater than zero", 400));

        Assert.That(status, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(ExtractMessage(body), Is.EqualTo("limit amount must be greater than zero"));
    }

    [Test]
    public async Task BusinessException_KeepsMessageForFrontendI18n()
    {
        var (status, body) = await InvokeWithAsync(() => throw new ArgumentException("account is already exist"));

        Assert.That(status, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(ExtractMessage(body), Is.EqualTo("account is already exist"));
    }

    [Test]
    public async Task UnknownException_ReturnsGeneric500()
    {
        var (status, body) = await InvokeWithAsync(() => throw new Exception("secret internal detail"));

        Assert.That(status, Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(ExtractMessage(body), Is.EqualTo("An error occurred while processing your request"));
        Assert.That(body, Does.Not.Contain("secret internal detail"));
    }
}
