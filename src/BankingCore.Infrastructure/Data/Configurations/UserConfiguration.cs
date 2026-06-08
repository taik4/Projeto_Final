using BankingCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingCore.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade User.
/// Mapeada para a tabela 'users' no MySQL (adicionada via db/init.sql).
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        // Pomelo EF Core mapeia Guid para CHAR(36) por padrão.
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.CpfHash)
            .HasColumnName("cpf_hash")
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(u => u.CpfHash)
            .IsUnique();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(u => u.AccountId)
            .HasColumnName("account_id")
            .HasColumnType("char(36)");

        // FK: users.account_id → accounts.account_id (1:1 opcional).
        // WithOne() sem navigation do lado Account (evita circular dependency).
        builder.HasOne(u => u.Account)
            .WithOne()
            .HasForeignKey<User>(u => u.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
