namespace BankingCore.Domain.Exceptions;

/// <summary>
/// Lançada quando credenciais inválidas ou token expirado.
/// Mapeada para HTTP 401 no ExceptionMiddleware.
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Credenciais inválidas.")
        : base(message) { }
}
