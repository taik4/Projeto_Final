using BankingCore.Domain.Entities;
using BankingCore.Domain.Interfaces;
using BankingCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankingCore.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de usuários usando Entity Framework Core.
/// Todos os métodos são async com CancellationToken (CONSTITUTION Lei III.3).
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly BankingDbContext _context;

    public UserRepository(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByCpfHashAsync(string cpfHash, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CpfHash == cpfHash, ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users
            .AnyAsync(u => u.Email == email, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task LinkAccountAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        // Carrega com tracking (sem AsNoTracking) para que o EF Core detecte a mudança
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException($"Usuário {userId} não encontrado ao vincular conta.");

        // Usa o método de domínio da entidade User (sem reflection)
        user.LinkAccount(accountId);

        await _context.SaveChangesAsync(ct);
    }
}
