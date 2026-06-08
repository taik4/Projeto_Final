using BankingCore.IntegrationTests.Fixtures;
using BankingCore.Domain.Entities;
using BankingCore.Domain.Utils;
using MySqlConnector;
using Dapper;
using BCrypt.Net;

namespace BankingCore.IntegrationTests.Base;

/// <summary>
/// Classe base para testes de integração que fornece métodos auxiliares
/// para manipulação do banco de dados e criação de dados de teste.
/// </summary>
[Collection("Database")]
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly MySqlDatabaseFixture Database;
    private readonly List<Guid> _usersToCleanup = new();
    private readonly List<Guid> _accountsToCleanup = new();
    private readonly List<long> _transactionsToCleanup = new();

    protected IntegrationTestBase(MySqlDatabaseFixture database)
    {
        Database = database;
    }

    protected MySqlConnection CreateConnection()
    {
        return Database.CreateConnection();
    }

    /// <summary>
    /// Cria um usuário de teste com dados válidos no banco.
    /// </summary>
    protected async Task<User> CreateTestUserAsync(
        string email = null!,
        string password = "TestPassword123!",
        string cpf = "12345678901")
    {
        email ??= $"testuser.{Guid.NewGuid():N}@example.com";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
        var cpfHash = Sha256Helper.Compute(cpf);

        using var connection = CreateConnection();
        var userId = Guid.NewGuid();

        await connection.ExecuteAsync(@"
            INSERT INTO users (id, email, password_hash, cpf_hash, created_at, updated_at)
            VALUES (@Id, @Email, @PasswordHash, @CpfHash, @CreatedAt, @UpdatedAt)",
            new
            {
                Id = userId.ToString(),
                Email = email,
                PasswordHash = passwordHash,
                CpfHash = cpfHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        _usersToCleanup.Add(userId);

        return new User(email, passwordHash, cpfHash);
    }

    /// <summary>
    /// Cria uma conta de teste vinculada a um usuário com saldo inicial.
    /// </summary>
    protected async Task<Account> CreateTestAccountAsync(
        Guid userId,
        decimal initialBalance = 0m,
        string holderName = null!)
    {
        holderName ??= "Test Holder";
        var accountId = Guid.NewGuid();
        var email = $"holder.{Guid.NewGuid():N}@example.com";
        var cpfHash = Sha256Helper.Compute("98765432109");

        using var connection = CreateConnection();

        await connection.ExecuteAsync(@"
            INSERT INTO accounts (account_id, holder_name, holder_email, holder_cpf_hash, balance, status, created_at, updated_at, user_id)
            VALUES (@AccountId, @HolderName, @HolderEmail, @HolderCpfHash, @Balance, 'ACTIVE', @CreatedAt, @UpdatedAt, @UserId)",
            new
            {
                AccountId = accountId,
                HolderName = holderName,
                HolderEmail = email,
                HolderCpfHash = cpfHash,
                Balance = initialBalance,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = userId.ToString()
            });

        // Atualiza o usuário para referenciar a conta
        await connection.ExecuteAsync(@"
            UPDATE users 
            SET account_id = @AccountId, updated_at = @UpdatedAt
            WHERE id = @UserId",
            new
            {
                AccountId = accountId.ToString(),
                UpdatedAt = DateTime.UtcNow,
                UserId = userId.ToString()
            });

        _accountsToCleanup.Add(accountId);

        return new Account(
            accountId: accountId,
            userId: userId,
            holderName: holderName,
            holderEmail: email,
            holderCpfHash: System.Text.Encoding.UTF8.GetBytes(cpfHash)
        );
    }

    /// <summary>
    /// Obtém o saldo atual de uma conta diretamente do banco.
    /// </summary>
    protected async Task<decimal> GetAccountBalanceAsync(Guid accountId)
    {
        using var connection = CreateConnection();
        var balance = await connection.ExecuteScalarAsync<decimal>(
            "SELECT balance FROM accounts WHERE account_id = @AccountId",
            new { AccountId = accountId.ToString() });

        return balance;
    }

    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
    }

    private async Task CleanupAsync()
    {
        using var connection = CreateConnection();

        // Remove transações
        foreach (var transactionId in _transactionsToCleanup)
        {
            await connection.ExecuteAsync(
                "DELETE FROM transactions WHERE transaction_id = @Id",
                new { Id = transactionId });
        }

        // Remove contas
        foreach (var accountId in _accountsToCleanup)
        {
            await connection.ExecuteAsync(
                "DELETE FROM accounts WHERE account_id = @AccountId",
                new { AccountId = accountId });
        }

        // Remove usuários
        foreach (var userId in _usersToCleanup)
        {
            await connection.ExecuteAsync(
                "DELETE FROM users WHERE id = @Id",
                new { Id = userId });
        }
    }
}
