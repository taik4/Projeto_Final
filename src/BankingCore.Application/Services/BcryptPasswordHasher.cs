using BankingCore.Domain.Interfaces;

namespace BankingCore.Application.Services;

/// <summary>
/// Implementação de hashing de senhas usando BCrypt (biblioteca BCrypt.Net-Next).
/// Work factor = 12 (balance entre segurança e performance para CPUs modernas).
/// (CONSTITUTION Lei I.1: Nunca confie no cliente — senha sempre hasheada)
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Work factor do BCrypt. 12 é o recomendado para 2024+.
    /// Cada incremento dobra o tempo de hash (~250ms em CPU moderna).
    /// </summary>
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Senha não pode ser vazia.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            // Hash malformado ou corrupto — trata como falha de autenticação
            return false;
        }
    }
}
