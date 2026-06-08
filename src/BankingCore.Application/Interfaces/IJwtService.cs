using BankingCore.Domain.Entities;

namespace BankingCore.Application.Interfaces;

/// <summary>
/// Contrato do serviço de geração e validação de JWT.
/// Implementado com RS256 (assimetria) para maior segurança.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Gera um access token JWT assinado com RS256.
    /// Contém claims: sub (userId), email, e optionally role.
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Gera um refresh token (string opaca, não JWT) para renovação.
    /// </summary>
    string GenerateRefreshToken();
}
