namespace BankingCore.Application.Settings;

/// <summary>
/// Configurações para geração e validação de JWT RS256.
/// Mapeada a partir da seção "JwtSettings" do appsettings.json.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Emissor do token (claim "iss"). Deve ser consistente entre geração e validação.
    /// </summary>
    public string Issuer { get; set; } = "LiceBank";

    /// <summary>
    /// Audiência do token (claim "aud"). Aplicações client que consomem o token.
    /// </summary>
    public string Audience { get; set; } = "LiceBankClient";

    /// <summary>
    /// Tempo de vida do access token em minutos. Após isso, o client deve renovar.
    /// CONSTITUTION Lei I: tokens curtos (15 min) para minimizar risco de vazamento.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Tempo de vida do refresh token em dias.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Caminho absoluto para o certificado/chave privada (PFX/PEM) em produção.
    /// Em dev (null), o RSA é gerado em memória automaticamente.
    /// CONSTITUTION Lei V.2: Secrets fora do código — caminho vem de variável de ambiente.
    /// </summary>
    public string? PrivateKeyPath { get; set; }
}
