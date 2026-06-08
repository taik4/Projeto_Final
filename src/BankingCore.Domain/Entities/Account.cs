using BankingCore.Domain.Enums;

namespace BankingCore.Domain.Entities;

/// <summary>
/// Entidade Account — representa uma conta bancária no sistema.
/// Mapeada para a tabela 'accounts' no banco (BINARY(16) UUID).
/// </summary>
public class Account
{
    public Guid AccountId { get; private set; }

    /// <summary>
    /// FK para o usuário dono da conta (1:1).
    /// Nullable: contas de seed podem existir sem usuário vinculado.
    /// CONSTITUTION Lei I.4: Autorização explícita — todo acesso deve verificar User.Id == Account.UserId.
    /// </summary>
    public Guid? UserId { get; private set; }

    public string HolderName { get; private set; } = string.Empty;
    public string HolderEmail { get; private set; } = string.Empty;
    public byte[] HolderCpfHash { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// Saldo disponível em BRL. RN03: nunca negativo (sem cheque especial).
    /// </summary>
    public decimal Balance { get; private set; }

    /// <summary>
    /// Status do ciclo de vida da conta.
    /// </summary>
    public AccountStatus Status { get; private set; } = AccountStatus.Active;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    // Constructor privado para EF Core
    private Account() { }

    /// <summary>
    /// Cria uma nova conta bancária vinculada a um usuário.
    /// </summary>
    public Account(
        Guid accountId,
        Guid userId,
        string holderName,
        string holderEmail,
        byte[] holderCpfHash)
    {
        AccountId = accountId;
        UserId = userId;
        HolderName = holderName ?? throw new ArgumentNullException(nameof(holderName));
        HolderEmail = holderEmail ?? throw new ArgumentNullException(nameof(holderEmail));
        HolderCpfHash = holderCpfHash ?? throw new ArgumentNullException(nameof(holderCpfHash));
        Balance = 0m;
        Status = AccountStatus.Active;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza o status da conta (bloquear/ativar).
    /// </summary>
    public void UpdateStatus(AccountStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        if (newStatus == AccountStatus.Closed)
            DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft delete — muda status para Closed e registra deleted_at.
    /// </summary>
    public void SoftDelete()
    {
        UpdateStatus(AccountStatus.Closed);
    }

    /// <summary>
    /// Adiciona saldo à conta. Usado para carregamento inicial de saldo em dev/test.
    /// </summary>
    public void AddBalance(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Valor deve ser maior que zero.", nameof(amount));

        Balance += amount;
        UpdatedAt = DateTime.UtcNow;
    }
}
