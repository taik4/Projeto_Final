namespace BankingCore.Domain.Exceptions;

/// <summary>
/// Lançada quando a requisição viola uma regra de negócio de validação.
/// Mapeada para HTTP 422 no ExceptionMiddleware.
/// </summary>
public class ValidationException : DomainException
{
    public IEnumerable<string> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new[] { message };
    }

    public ValidationException(IEnumerable<string> errors)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors;
    }
}
