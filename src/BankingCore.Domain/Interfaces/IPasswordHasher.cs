namespace BankingCore.Domain.Interfaces;

/// <summary>
/// Contrato para hashing e verificação de senhas.
/// Implementado com BCrypt na camada Application.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Gera um hash seguro da senha usando BCrypt (work factor 12).
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifica se a senha fornecida corresponde ao hash armazenado.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
