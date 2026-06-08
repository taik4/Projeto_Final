namespace BankingCore.Domain.Exceptions;

/// <summary>
/// Exceção lançada quando um recurso não é encontrado.
/// Mapeada para HTTP 404 no ExceptionMiddleware.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string resource)
        : base($"{resource} não encontrado.") { }
}
