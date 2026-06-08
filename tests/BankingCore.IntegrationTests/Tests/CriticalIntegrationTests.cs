using BankingCore.IntegrationTests.Fixtures;
using FluentAssertions;
using Dapper;
using MySqlConnector;
using Xunit;

namespace BankingCore.IntegrationTests.Tests;

/// <summary>
/// Testes de integração cobrindo os 5 casos críticos do sistema bancário.
/// Cada teste valida um fluxo completo contra o MySQL real via Testcontainers.
///
/// Stored Procedure: sp_process_pix_transfer
///   IN:  p_end_to_end_id, p_idempotency_key, p_source_account_id, p_target_account_id,
///        p_amount, p_description, p_receiver_name, p_receiver_doc_masked
///   OUT: p_result_code (0=OK, 1=Idempotente, 2=Saldo insuficiente, 4=Não encontrada),
///        p_result_message, p_existing_e2e_id
/// </summary>
[Collection("Database")]
public class CriticalIntegrationTests : IDisposable
{
    private readonly MySqlDatabaseFixture _db;
    private readonly List<string> _cleanupAccountIds = new();

    public CriticalIntegrationTests(MySqlDatabaseFixture db)
    {
        _db = db;
    }

    public void Dispose()
    {
        using var connection = _db.CreateConnection();
        foreach (var id in _cleanupAccountIds)
        {
            connection.Execute("DELETE FROM audit_log WHERE source_account_id = @Id OR target_account_id = @Id", new { Id = id });
            connection.Execute("DELETE FROM transactions WHERE source_account_id = @Id OR target_account_id = @Id", new { Id = id });
            connection.Execute("UPDATE users SET account_id = NULL WHERE account_id = @Id", new { Id = id });
            connection.Execute("DELETE FROM accounts WHERE account_id = @Id", new { Id = id });
        }
    }

    /// <summary>
    /// TESTE 1: Criação de conta com sucesso.
    /// Valida saldo zero, status ACTIVE e vínculo com o usuário.
    /// </summary>
    [Fact]
    public async Task AccountCreation_ShouldCreateAccount_WithInitialBalanceZero()
    {
        var userId = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid().ToString();
        var email = $"test.{Guid.NewGuid():N}@example.com";

        using var connection = _db.CreateConnection();

        // Act — criar conta primeiro (FK users→accounts)
        await connection.ExecuteAsync(@"
            INSERT INTO accounts (account_id, holder_name, holder_email, holder_cpf_hash, balance, status)
            VALUES (@AccountId, 'Test Holder', @Email, @CpfHash, 0.00, 'ACTIVE')",
            new { AccountId = accountId, Email = email, CpfHash = Sha256Bytes("cpf-teste-1") });
        _cleanupAccountIds.Add(accountId);

        // Criar usuário vinculado à conta
        await connection.ExecuteAsync(@"
            INSERT INTO users (id, email, cpf_hash, password_hash, account_id)
            VALUES (@Id, @Email, @CpfHash, @PasswordHash, @AccountId)",
            new
            {
                Id = userId,
                Email = email,
                CpfHash = Sha256Hex("cpf-teste-1"),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!", 10),
                AccountId = accountId
            });

        // Assert
        var row = await connection.QuerySingleAsync(
            @"SELECT a.balance, a.status, u.id AS linked_user_id
              FROM accounts a
              LEFT JOIN users u ON u.account_id = a.account_id
              WHERE a.account_id = @AccountId",
            new { AccountId = accountId });

        ((decimal)row.balance).Should().Be(0.00m, "saldo inicial deve ser zero");
        ((string)row.status).Should().Be("ACTIVE", "status inicial deve ser ACTIVE");
        Assert.Equal(userId, row.linked_user_id.ToString());
    }

    /// <summary>
    /// TESTE 2: Autenticação com credenciais válidas.
    /// Valida que senha é verificada corretamente via BCrypt.
    /// </summary>
    [Fact]
    public async Task Authentication_WithValidCredentials_ShouldValidatePassword()
    {
        var userId = Guid.NewGuid().ToString();
        var email = $"auth.{Guid.NewGuid():N}@example.com";
        var plainPassword = "MySecurePass456!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword, 12);

        using var connection = _db.CreateConnection();

        var accountId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(@"
            INSERT INTO accounts (account_id, holder_name, holder_email, holder_cpf_hash, balance, status)
            VALUES (@AccountId, 'Auth User', @Email, @CpfHash, 0.00, 'ACTIVE')",
            new { AccountId = accountId, Email = email, CpfHash = Sha256Bytes("cpf-auth-test") });
        _cleanupAccountIds.Add(accountId);

        await connection.ExecuteAsync(@"
            INSERT INTO users (id, email, cpf_hash, password_hash, account_id)
            VALUES (@Id, @Email, @CpfHash, @PasswordHash, @AccountId)",
            new
            {
                Id = userId,
                Email = email,
                CpfHash = Sha256Hex("cpf-auth-test"),
                PasswordHash = passwordHash,
                AccountId = accountId
            });

        // Act
        var storedHash = await connection.ExecuteScalarAsync<string>(
            "SELECT password_hash FROM users WHERE email = @Email",
            new { Email = email });

        // Assert
        storedHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify(plainPassword, storedHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("WrongPassword", storedHash).Should().BeFalse();
    }

    /// <summary>
    /// TESTE 3: Transferência PIX com saldo suficiente.
    /// Valida result_code=0, débito na origem, crédito no destino e par DEBIT/CREDIT.
    /// </summary>
    [Fact]
    public async Task PixTransfer_WithSufficientBalance_ShouldCompleteSuccessfully()
    {
        using var connection = _db.CreateConnection();
        var sourceId = await CreateAccountAsync(connection, "Sender", 1000.00m);
        var targetId = await CreateAccountAsync(connection, "Receiver", 500.00m);

        var endToEndId = GenerateE2eId();
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act
        var (resultCode, resultMessage) = await CallPixTransfer(
            connection, endToEndId, idempotencyKey,
            sourceId, targetId, 300.00m,
            "Integration test", "Re***", "***.***.***-00");

        // Assert
        resultCode.Should().Be(0, $"deve completar com sucesso: {resultMessage}");

        (await GetBalanceAsync(connection, sourceId)).Should().Be(700.00m);
        (await GetBalanceAsync(connection, targetId)).Should().Be(800.00m);

        var txCount = await GetTransactionCount(connection, endToEndId, idempotencyKey);
        txCount.Should().Be(2, "um par DEBIT + CREDIT");
    }

    /// <summary>
    /// TESTE 4: Transferência PIX com saldo insuficiente.
    /// Valida result_code=2, saldos inalterados e nenhuma transação criada.
    /// </summary>
    [Fact]
    public async Task PixTransfer_WithInsufficientBalance_ShouldBeRejected()
    {
        using var connection = _db.CreateConnection();
        var sourceId = await CreateAccountAsync(connection, "Poor Sender", 100.00m);
        var targetId = await CreateAccountAsync(connection, "Receiver", 500.00m);

        var endToEndId = GenerateE2eId();
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act — tenta transferir R$500 de uma conta com R$100
        var (resultCode, resultMessage) = await CallPixTransfer(
            connection, endToEndId, idempotencyKey,
            sourceId, targetId, 500.00m,
            "Should fail", "Re***", "***.***.***-00");

        // Assert
        resultCode.Should().Be(2, $"deve rejeitar por saldo insuficiente: {resultMessage}");
        resultMessage.Should().Contain("Saldo insuficiente");

        (await GetBalanceAsync(connection, sourceId)).Should().Be(100.00m, "saldo inalterado");
        (await GetBalanceAsync(connection, targetId)).Should().Be(500.00m, "saldo inalterado");

        var txCount = await GetTransactionCount(connection, endToEndId, idempotencyKey);
        txCount.Should().Be(0, "nenhuma transação deve ser criada");
    }

    /// <summary>
    /// TESTE 5: Idempotência — mesma transferência não debita duas vezes.
    /// Valida result_code=1 na segunda chamada, saldo correto e apenas 1 par.
    /// </summary>
    [Fact]
    public async Task PixTransfer_WithSameIdempotencyKey_ShouldPreventDoubleDebit()
    {
        using var connection = _db.CreateConnection();
        var sourceId = await CreateAccountAsync(connection, "Idem Sender", 1000.00m);
        var targetId = await CreateAccountAsync(connection, "Idem Receiver", 500.00m);

        var endToEndId = GenerateE2eId();
        var idempotencyKey = Guid.NewGuid().ToString();

        // Primeira transferência (sucesso)
        var (code1, msg1) = await CallPixTransfer(
            connection, endToEndId, idempotencyKey,
            sourceId, targetId, 200.00m,
            "First call", "Re***", "***.***.***-00");

        code1.Should().Be(0, $"primeira deve completar: {msg1}");
        (await GetBalanceAsync(connection, sourceId)).Should().Be(800.00m);

        // Segunda com MESMO idempotency key
        var (code2, msg2) = await CallPixTransfer(
            connection, endToEndId, idempotencyKey,
            sourceId, targetId, 200.00m,
            "Retry", "Re***", "***.***.***-00");

        // Assert
        code2.Should().Be(1, $"segunda deve retornar idempotente: {msg2}");
        (await GetBalanceAsync(connection, sourceId)).Should().Be(800.00m, "sem double-debit");
        (await GetBalanceAsync(connection, targetId)).Should().Be(700.00m, "sem double-credit");

        var txCount = await GetTransactionCount(connection, endToEndId, idempotencyKey);
        txCount.Should().Be(2, "apenas 1 par DEBIT/CREDIT");
    }

    // ──────────────────── Helpers ────────────────────

    private async Task<string> CreateAccountAsync(MySqlConnection conn, string name, decimal balance)
    {
        var accountId = Guid.NewGuid().ToString();
        await conn.ExecuteAsync(@"
            INSERT INTO accounts (account_id, holder_name, holder_email, holder_cpf_hash, balance, status)
            VALUES (@AccountId, @Name, @Email, @CpfHash, @Balance, 'ACTIVE')",
            new
            {
                AccountId = accountId,
                Name = name,
                Email = $"{name.ToLower().Replace(" ", ".")}.{Guid.NewGuid():N}@example.com",
                CpfHash = Sha256Bytes($"cpf-{accountId}"),
                Balance = balance
            });
        _cleanupAccountIds.Add(accountId);
        return accountId;
    }

    private static async Task<decimal> GetBalanceAsync(MySqlConnection conn, string accountId)
    {
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT balance FROM accounts WHERE account_id = @Id",
            new { Id = accountId });
    }

    private static async Task<int> GetTransactionCount(MySqlConnection conn, string endToEndId, string idempotencyKey)
    {
        // Conta DEBIT (com idempotency_key) + CREDIT (E2E com sufixo 'C')
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM transactions WHERE idempotency_key = @Key OR (end_to_end_id = @E2e OR end_to_end_id = @E2eC)",
            new { Key = idempotencyKey, E2e = endToEndId, E2eC = endToEndId[..^1] + "C" });
    }

    private static string GenerateE2eId()
    {
        // 32 chars: 'E' + 31 hex chars de um GUID
        return $"E{Guid.NewGuid():N}"[..32];
    }

    private static byte[] Sha256Bytes(string input)
    {
        return System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
    }

    private static string Sha256Hex(string input)
    {
        return Convert.ToHexString(Sha256Bytes(input)).ToLowerInvariant();
    }

    private static async Task<(int ResultCode, string ResultMessage)> CallPixTransfer(
        MySqlConnection connection,
        string endToEndId,
        string idempotencyKey,
        string sourceAccountId,
        string targetAccountId,
        decimal amount,
        string description,
        string receiverName,
        string receiverDocMasked)
    {
        // MySqlConnector não suporta ParameterDirection.Output com CommandType.Text.
        // Solução: usar variáveis de sessão MySQL como OUT params.
        await connection.OpenAsync();

        // Inicializa variáveis de sessão para os OUT params
        await connection.ExecuteAsync("SET @_result_code = 0; SET @_result_message = ''; SET @_existing_e2e = NULL;");

        // Call SP com variáveis de sessão como OUT params
        await connection.ExecuteAsync(@"
            CALL sp_process_pix_transfer(
                @p_end_to_end_id, @p_idempotency_key,
                @p_source_account_id, @p_target_account_id,
                @p_amount, @p_description,
                @p_receiver_name, @p_receiver_doc_masked,
                @_result_code, @_result_message, @_existing_e2e)",
            new
            {
                p_end_to_end_id = endToEndId,
                p_idempotency_key = idempotencyKey,
                p_source_account_id = sourceAccountId,
                p_target_account_id = targetAccountId,
                p_amount = amount,
                p_description = description,
                p_receiver_name = receiverName,
                p_receiver_doc_masked = receiverDocMasked
            });

        // Lê os resultados das variáveis de sessão
        var result = await connection.QuerySingleAsync(
            "SELECT @_result_code AS code, @_result_message AS message");

        connection.Close();

        return (Convert.ToInt32(result.code), result.message?.ToString() ?? "");
    }
}
