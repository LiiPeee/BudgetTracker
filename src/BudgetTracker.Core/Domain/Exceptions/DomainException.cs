namespace BudgetTracker.Core.Domain.Exceptions;

/// <summary>
/// Exceção de domínio com mensagem segura para o cliente, status HTTP explícito
/// e código de erro estável (para o frontend traduzir sem depender do wording).
/// Use para erros de negócio intencionais; nunca para detalhes de infraestrutura
/// (banco, APIs externas), que devem permanecer apenas no log.
/// </summary>
public class DomainException : Exception
{
    public int StatusCode { get; }
    public string? Code { get; }

    public DomainException(string message, int statusCode = 400, string? code = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
