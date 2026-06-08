using BankingCore.Domain.Entities;

namespace BankingCore.Domain.Interfaces;

/// <summary>
/// Contrato de repositório para contas bancárias.
/// Implementado com EF Core na camada Infrastructure.
/// CONSTITUTION Lei II.4: ORM para CRUD, Dapper para dinheiro (transferências).
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Cria uma nova conta no banco (INSERT).
    /// </summary>
    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca conta por seu AccountId (UUID binário). Retorna null se não encontrar.
    /// Ignora contas com soft delete (DeletedAt != null) por padrão.
    /// </summary>
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca conta pelo UserId do dono. Um usuário pode ter no máximo uma conta ativa.
    /// Retorna null se não encontrar.
    /// </summary>
    Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste alterações na conta (UPDATE).
    /// </summary>
    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se já existe conta ativa para o usuário.
    /// </summary>
    Task<bool> ExistsActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca conta por ID com tracking habilitado (para UPDATE).
    /// Deve ser usada apenas dentro de um fluxo de transação atômica.
    /// </summary>
    Task<Account?> GetTrackedByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todas as contas do sistema. Útil para dev/admin.
    /// </summary>
    Task<IEnumerable<Account>> GetAllAsync(CancellationToken cancellationToken = default);
}
