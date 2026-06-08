using BankingCore.Domain.Exceptions;

namespace BankingCore.Domain.Entities;

/// <summary>
/// Entidade de usuário do sistema.
/// Armazena credenciais de autenticação e vínculo com a conta bancária.
/// CONSTITUTION Lei I.2: CPF armazenado apenas como hash SHA-256.
/// </summary>
public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 do CPF em formato hex. Nunca armazena CPF pleno.
    /// </summary>
    public string CpfHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Vínculo opcional com a conta bancária principal do usuário.
    /// </summary>
    public Guid? AccountId { get; private set; }
    public Account? Account { get; private set; }

    private User() { } // EF Core

    public User(string email, string passwordHash, string cpfHash, Guid? accountId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email é obrigatório.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("PasswordHash é obrigatório.");
        if (string.IsNullOrWhiteSpace(cpfHash))
            throw new DomainException("CpfHash é obrigatório.");

        Id = Guid.NewGuid();
        Email = email.Trim().ToLower();
        PasswordHash = passwordHash;
        CpfHash = cpfHash;
        AccountId = accountId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Vincula uma conta ao usuário (atualiza users.account_id).
    /// Usado pelo CreateAccountUseCase após criar uma nova conta.
    /// </summary>
    public void LinkAccount(Guid accountId)
    {
        AccountId = accountId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Desvincula a conta (quando a conta é encerrada).
    /// </summary>
    public void UnlinkAccount()
    {
        AccountId = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
