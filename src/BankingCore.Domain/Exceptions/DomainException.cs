namespace BankingCore.Domain.Exceptions;

/// <summary>
/// Exceção para falhas de regra de negócio (domínio).
/// Mapeada para HTTP 400/409 no ExceptionMiddleware (CONSTITUTION Lei III.1).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}
