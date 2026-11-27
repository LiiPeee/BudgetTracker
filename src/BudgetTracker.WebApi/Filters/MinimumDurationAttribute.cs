using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BudgetTracker.WebApi.Filters;

/// <summary>
/// Segura a resposta até um tempo mínimo. Usado onde só parte dos pedidos envia e-mail, para que
/// a demora não revele se a conta existe. Envios mais lentos que o mínimo ainda se destacam.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MinimumDurationAttribute(int milliseconds) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var elapsed = Stopwatch.StartNew();

        // Exceções da action ficam no contexto e são relançadas depois do filtro, já com a espera.
        await next();

        var remaining = TimeSpan.FromMilliseconds(milliseconds) - elapsed.Elapsed;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining);
    }
}
