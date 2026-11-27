namespace BudgetTracker.Core.Domain.Utils;

/// <summary>
/// Data e hora no fuso dos usuários. O mês de competência é o do calendário deles:
/// em UTC, um lançamento feito às 22h do dia 31 cairia no mês seguinte.
/// </summary>
public static class AppClock
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static DateTime Now => ToLocal(DateTime.UtcNow);

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Imagem sem tzdata: Brasília é UTC-3 sem horário de verão desde 2019.
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
        }
    }
}
