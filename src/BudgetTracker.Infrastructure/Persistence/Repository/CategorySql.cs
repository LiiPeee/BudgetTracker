namespace BudgetTracker.Infrastructure.Persistence.Repository;

/// <summary>
/// Compara nomes de categoria sem diferenciar acento, caixa, espaço ou "_":
/// "Alimentação" e "ALIMENTACAO" são a mesma categoria.
/// </summary>
internal static class CategorySql
{
    private const string From = "áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ -";
    private const string To = "aaaaaeeeeiiiiooooouuuucAAAAAEEEEIIIIOOOOOUUUUC__";

    public static string Normalize(string expression) => $"UPPER(TRANSLATE({expression}, '{From}', '{To}'))";
}
