using System.Security.Claims;

namespace BankingCore.API.Extensions;

/// <summary>
/// Extensões para HttpContext que facilitam acesso às claims do JWT autenticado.
/// Evita espalhar parsing de claims por múltiplos controllers.
/// </summary>
public static class HttpContextClaimsExtensions
{
    /// <summary>
    /// Retorna o UserId do JWT (claim "sub") como Guid.
    /// Lança UnauthorizedException se o usuário não está autenticado ou o claim é inválido.
    /// </summary>
    public static Guid GetUserId(this HttpContext httpContext)
    {
        var subClaim = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(subClaim) || !Guid.TryParse(subClaim, out var userId))
            throw new BankingCore.Domain.Exceptions.UnauthorizedException("Token JWT inválido ou ausente.");

        return userId;
    }
}

file static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
}
