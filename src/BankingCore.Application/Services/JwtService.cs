using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using BankingCore.Application.Interfaces;
using BankingCore.Application.Settings;
using BankingCore.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BankingCore.Application.Services;

/// <summary>
/// Implementação do serviço JWT usando RS256 (RSA assimétrico).
///
/// Em DESENVOLVIMENTO: gera um par RSA em memória (não persistente entre restarts).
/// Em PRODUÇÃO: carrega a chave privada de um arquivo PFX/PEM configurado em JwtSettings.PrivateKeyPath.
/// (CONSTITUTION Lei V.2: Secrets fora do código)
///
/// O cliente (API) configura AddJwtBearer com a chave pública extraída do certificado.
/// </summary>
public sealed class JwtService : IJwtService, IDisposable
{
    private readonly JwtSettings _settings;
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _securityKey;
    private readonly SigningCredentials _signingCredentials;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        _rsa = RSA.Create(2048);

        // Em produção: carregar chave privada de arquivo
        if (!string.IsNullOrEmpty(_settings.PrivateKeyPath) && File.Exists(_settings.PrivateKeyPath))
        {
            // Carrega chave privada de arquivo PEM (ex: private_key.pem)
            var privateKeyPem = File.ReadAllText(_settings.PrivateKeyPath);
            _rsa.ImportFromPem(privateKeyPem);
        }
        // Caso contrário: RSA em memória (apenas para desenvolvimento)

        _securityKey = new RsaSecurityKey(_rsa);
        _signingCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.RsaSha256);
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // ID da conta vinculada (permite autorização por policy na API)
            new("account_id", user.AccountId?.ToString() ?? string.Empty)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = _signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Expõe a chave pública de validação (usada pela API para configurar AddJwtBearer).
    /// </summary>
    public RsaSecurityKey GetSecurityKey() => _securityKey;

    public void Dispose()
    {
        _rsa.Dispose();
    }
}
