using BankingCore.Domain.Entities;
using BankingCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BankingCore.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade Account.
/// Mapeada para a tabela 'accounts' criada em db/init.sql (Fase 1).
/// </summary>
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.AccountId);

        builder.Property(a => a.AccountId)
            .HasColumnName("account_id")
            .HasColumnType("char(36)");

        // FK denormalizada para User (user_id é CHAR(36) no banco, igual a users.id).
        // Nullable: contas de seed podem existir sem usuário vinculado.
        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .HasColumnType("char(36)");

        builder.Property(a => a.HolderName)
            .HasColumnName("holder_name")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(a => a.HolderEmail)
            .HasColumnName("holder_email")
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(a => a.HolderEmail)
            .IsUnique();

        builder.Property(a => a.HolderCpfHash)
            .HasColumnName("holder_cpf_hash")
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasColumnName("balance")
            .HasColumnType("decimal(15,2)");

        // Status ENUM: converte AccountStatus enum <-> string MySQL ENUM
        var statusConverter = new ValueConverter<AccountStatus, string>(
            v => v.ToString().ToUpperInvariant(),
            v => Enum.Parse<AccountStatus>(v, true));

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        // UserId é um campo de denormalização para queries (não é FK real no banco).
        // A FK real está em users.account_id → accounts.account_id (UserConfiguration).
        // Mantemos user_id no banco apenas para evitar JOINs em queries frequentes.
    }
}
