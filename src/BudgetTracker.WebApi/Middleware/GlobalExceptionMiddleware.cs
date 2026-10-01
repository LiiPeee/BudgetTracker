using System.Text.Json;
using BudgetTracker.Core.Domain.Exceptions;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing the request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // DomainException carries an intentional, client-safe message.
        // Business exceptions (ArgumentException/UnauthorizedAccessException/KeyNotFoundException)
        // also expose their message — the frontend relies on those exact strings for i18n.
        // Infrastructure exceptions (InvalidOperationException and anything else) NEVER leak
        // their raw .Message in production (DB/API internals stay in the log only).
        var (statusCode, safeMessage, errorCode) = exception switch
        {
            DomainException domain => (domain.StatusCode, domain.Message, domain.Code),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message, "bad_request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, exception.Message, "unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message, "not_found"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "An error occurred while processing your request", "conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An error occurred while processing your request", "internal")
        };
        context.Response.StatusCode = statusCode;

        var isDevelopment = context.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
        var response = new
        {
            error = new
            {
                code = errorCode,
                message = isDevelopment ? exception.Message : safeMessage,
                details = isDevelopment
                    ? exception.StackTrace
                    : "An error occurred while processing your request"
            }
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
