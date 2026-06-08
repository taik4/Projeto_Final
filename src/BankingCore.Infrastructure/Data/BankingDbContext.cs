using BankingCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankingCore.Infrastructure.Data;

/// <summary>
/// DbContext do Banking Core.
/// Mapeado para MySQL 8.0 via Pomelo.EntityFrameworkCore.MySql.
/// Usado para CRUD de User e Account (CONSTITUTION Lei II.4: ORM para CRUD, Dapper para dinheiro).
/// </summary>
public class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankingDbContext).Assembly);
    }
}
