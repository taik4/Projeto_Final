using BankingCore.Domain.Entities;

namespace BankingCore.Domain.Interfaces;

/// <summary>
/// Contrato de repositório para usuários.
/// Implementado com EF Core na camada Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByCpfHashAsync(string cpfHash, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Vincula uma conta ao usuário (atualiza users.account_id).
    /// Chamado após criação de conta para manter a FK users.account_id consistente.
    /// </summary>
    Task LinkAccountAsync(Guid userId, Guid accountId, CancellationToken ct = default);
}
