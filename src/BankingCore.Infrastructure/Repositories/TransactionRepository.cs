using System.Data;
using System.Text;
using BankingCore.Application.DTOs;
using BankingCore.Domain.DTOs;
using BankingCore.Domain.Enums;
using BankingCore.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BankingCore.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de transações usando Dapper + Stored Procedure.
///
/// CONSTITUTION Lei II.4: "ORM para CRUD, Dapper para dinheiro".
/// O fluxo crítico de transferência PIX usa Dapper porque:
///   1. Delega toda a lógica financeira para a Stored Procedure (atomicidade + idempotência).
///   2. Evita múltiplas round-trips que o EF Core geraria (UPDATE → UPDATE → INSERT → INSERT).
///   3. Garante que FOR UPDATE NOWAIT e TRANSACTION vivem dentro da SP, não no C#.
/// </summary>
public sealed class TransactionRepository : ITransactionRepository
{
    private readonly MySqlConnection _connection;
    private readonly ILogger<TransactionRepository> _logger;

    public TransactionRepository(MySqlConnection connection, ILogger<TransactionRepository> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PixTransferResult> ProcessTransferAsync(
        Guid sourceAccountId,
        Guid targetAccountId,
        decimal amount,
        string endToEndId,
        Guid idempotencyKey,
        string description,
        string receiverName,
        string receiverDocMasked,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_end_to_end_id", endToEndId, DbType.String, ParameterDirection.Input);
        parameters.Add("p_idempotency_key", idempotencyKey.ToString(), DbType.String, ParameterDirection.Input);
        parameters.Add("p_source_account_id", sourceAccountId.ToString(), DbType.String, ParameterDirection.Input);
        parameters.Add("p_target_account_id", targetAccountId.ToString(), DbType.String, ParameterDirection.Input);
        parameters.Add("p_amount", amount, DbType.Decimal, ParameterDirection.Input);
        parameters.Add("p_description", description, DbType.String, ParameterDirection.Input);
        parameters.Add("p_receiver_name", receiverName, DbType.String, ParameterDirection.Input);
        parameters.Add("p_receiver_doc_masked", receiverDocMasked, DbType.String, ParameterDirection.Input);

        parameters.Add("p_result_code", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("p_result_message", dbType: DbType.String, size: 255, direction: ParameterDirection.Output);
        parameters.Add("p_existing_e2e_id", dbType: DbType.String, size: 32, direction: ParameterDirection.Output);

        try
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync(cancellationToken);

            await _connection.ExecuteAsync(
                sql: "sp_process_pix_transfer",
                param: parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: 10);

            var resultCode = parameters.Get<int>("p_result_code");
            var resultMessage = parameters.Get<string>("p_result_message") ?? string.Empty;
            var existingE2E = parameters.Get<string>("p_existing_e2e_id");

            if (!Enum.IsDefined(typeof(PixResultCode), resultCode))
                resultCode = (int)PixResultCode.InternalError;

            return new PixTransferResult
            {
                ResultCode = (PixResultCode)resultCode,
                Message = resultMessage,
                EndToEndId = existingE2E
            };
        }
        catch (MySqlException ex) when (ex.Number == 3572)
        {
            _logger.LogWarning(ex, "Falha ao obter lock pessimista em transferência PIX. Source={Source}", sourceAccountId);
            return new PixTransferResult
            {
                ResultCode = PixResultCode.LockFailed,
                Message = "Sistema ocupado processando outra transferência. Tente novamente em alguns instantes.",
                EndToEndId = null
            };
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Erro MySQL ao processar transferência PIX. Source={Source}, Target={Target}", sourceAccountId, targetAccountId);
            return new PixTransferResult
            {
                ResultCode = PixResultCode.InternalError,
                Message = "Falha interna ao processar transferência. Suporte foi notificado.",
                EndToEndId = null
            };
        }
    }

    /// <inheritdoc />
    public async Task<StatementResult> GetStatementAsync(
        StatementQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync(cancellationToken);

            var parameterDict = new DynamicParameters();
            parameterDict.Add("AccountId", query.AccountId.ToString());
            parameterDict.Add("Limit", query.Limit ?? 50);

            var whereConditions = new List<string>
            {
                "owner_account_id = @AccountId"
            };

            if (query.StartDate.HasValue)
            {
                whereConditions.Add("created_at >= @StartDate");
                parameterDict.Add("StartDate", query.StartDate.Value);
            }

            if (query.EndDate.HasValue)
            {
                whereConditions.Add("created_at <= @EndDate");
                parameterDict.Add("EndDate", query.EndDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Cursor))
            {
                whereConditions.Add("transaction_id < @Cursor");
                parameterDict.Add("Cursor", long.Parse(query.Cursor));
            }

            var sql = new StringBuilder();
            sql.AppendLine("SELECT");
            sql.AppendLine("  transaction_id,");
            sql.AppendLine("  end_to_end_id,");
            sql.AppendLine("  counterparty_account_id,");
            sql.AppendLine("  amount,");
            sql.AppendLine("  created_at,");
            sql.AppendLine("  status,");
            sql.AppendLine("  masked_receiver_name,");
            sql.AppendLine("  CASE WHEN source_account_id = @AccountId THEN 'DEBIT' ELSE 'CREDIT' END AS direction");
            sql.AppendLine("FROM vw_account_statement");
            sql.AppendLine("WHERE " + string.Join(" AND ", whereConditions));
            sql.AppendLine("ORDER BY transaction_id DESC");
            sql.AppendLine("LIMIT @Limit");

            var transactions = await _connection.QueryAsync<StatementTransactionRow>(
                sql: sql.ToString(),
                param: parameterDict,
                commandTimeout: 10);

            var transactionList = transactions.ToList();

            var nextCursor = transactionList.Count > 0
                ? transactionList.Last().transaction_id.ToString()
                : null;

            var hasMore = false;
            if (transactionList.Count > 0)
            {
                var checkSql = @"
                    SELECT COUNT(1) 
                    FROM vw_account_statement 
                    WHERE owner_account_id = @AccountId 
                      AND transaction_id < @LastId";
                
                parameterDict.Add("LastId", transactionList.Last().transaction_id);
                var additionalCount = await _connection.ExecuteScalarAsync<int>(
                    checkSql,
                    parameterDict);
                
                hasMore = additionalCount > 0;
            }

            var transactionInfos = transactionList.Select(t => new TransactionInfo(
                TransactionId: t.transaction_id,
                EndToEndId: t.end_to_end_id,
                Date: t.created_at,
                Direction: t.direction,
                Description: null,
                Amount: t.amount,
                Status: t.status,
                CounterpartyName: t.masked_receiver_name,
                CounterpartyDocument: t.counterparty_account_id?.ToString() ?? string.Empty
            )).ToList();

            return new StatementResult(
                Transactions: transactionInfos,
                NextCursor: nextCursor,
                HasMore: hasMore
            );
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Erro ao consultar extrato bancário. AccountId={AccountId}", query.AccountId);
            throw;
        }
    }

    private class StatementTransactionRow
    {
        public long transaction_id { get; set; }
        public string end_to_end_id { get; set; } = string.Empty;
        public Guid? counterparty_account_id { get; set; }
        public decimal amount { get; set; }
        public DateTime created_at { get; set; }
        public string status { get; set; } = string.Empty;
        public string masked_receiver_name { get; set; } = string.Empty;
        public string direction { get; set; } = string.Empty;
    }
}
