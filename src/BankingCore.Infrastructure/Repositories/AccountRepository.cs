using BankingCore.Domain.Entities;
using BankingCore.Domain.Enums;
using BankingCore.Domain.Interfaces;
using BankingCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankingCore.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de contas usando Entity Framework Core.
/// Todos os métodos são async com CancellationToken (CONSTITUTION Lei III.3).
/// Usado apenas para CRUD — transferências PIX usam Dapper + SP (Fase 4).
/// </summary>
public class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _context;

    public AccountRepository(BankingDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await _context.Accounts.AddAsync(account, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Status != AccountStatus.Closed)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AnyAsync(a => a.UserId == userId && a.Status == AccountStatus.Active, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Account?> GetTrackedByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        // Sem AsNoTracking — entidade fica attached ao context para UPDATE
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
