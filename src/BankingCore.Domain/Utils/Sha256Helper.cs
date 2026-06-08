using System.Security.Cryptography;
using System.Text;

namespace BankingCore.Domain.Utils;

/// <summary>
/// Utilitário para cálculo de hash SHA-256.
/// Usado para hashear CPF (CONSTITUTION Lei I.2: Zero PII em storage).
/// </summary>
public static class Sha256Helper
{
    /// <summary>
    /// Calcula o hash SHA-256 de um texto e retorna em formato hexadecimal (64 chars).
    /// Exemplo: Compute("12345678901") -> "6ca13d52ca70c883e0f0bb101e425a89e8624de51db2d4b31f02cbb23b99a357"
    /// </summary>
    public static string Compute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Calcula o hash SHA-256 e retorna os bytes (para armazenar em VARBINARY no MySQL).
    /// </summary>
    public static byte[] ComputeBytes(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
}
