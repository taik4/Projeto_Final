using BankingCore.Domain.DTOs;
using BankingCore.Domain.Enums;

namespace BankingCore.Domain.Interfaces;

/// <summary>
/// Resultado retornado pela Stored Procedure sp_process_pix_transfer.
/// Mapeia os parâmetros OUT: p_result_code, p_result_message, p_existing_e2e_id.
/// </summary>
public sealed class PixTransferResult
{
    /// <summary>Código de resultado tipado (0=Sucesso, 1=Idempotente, ...).</summary>
    public PixResultCode ResultCode { get; init; }

    /// <summary>Mensagem descritiva do banco (em português).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>EndToEndId da transação (novo ou existente se idempotente).</summary>
    public string? EndToEndId { get; init; }

    /// <summary>True quando a transferência foi efetivamente processada ou retorna resultado idempotente.</summary>
    public bool IsSuccess => ResultCode is PixResultCode.Success or PixResultCode.Idempotent;

    /// <summary>True se foi detectada requisição duplicada (idempotência ativada).</summary>
    public bool IsIdempotent => ResultCode == PixResultCode.Idempotent;
}

/// <summary>
/// Contrato de repositório para operações transacionais PIX.
/// Implementado com Dapper + SP (CONSTITUTION Lei II.4: Dapper para dinheiro, ORM para CRUD).
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Executa a transferência PIX chamando a Stored Procedure sp_process_pix_transfer.
    /// Não lança exceção para erros de negócio (saldo insuficiente, conta inativa, etc.) —
    /// esses são retornados via <see cref="PixTransferResult.ResultCode"/>.
    /// Exceções de infraestrutura (conexão, timeout) são propagadas.
    /// </summary>
    Task<PixTransferResult> ProcessTransferAsync(
        Guid sourceAccountId,
        Guid targetAccountId,
        decimal amount,
        string endToEndId,
        Guid idempotencyKey,
        string description,
        string receiverName,
        string receiverDocMasked,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta o extrato bancário de uma conta com paginação por cursor (Keyset).
    /// </summary>
    /// <param name="query">Parâmetros da consulta incluindo filtros e paginação</param>
    /// <returns>Lista paginada de transações com cursor para próxima página</returns>
    /// <remarks>
    /// **CONSTITUTION Lei II.4:** Dapper para operações de leitura de transações.
    /// 
    /// **RN04 - Transparência PIX:**
    /// - EndToEndId sempre retornado
    /// - Dados do counterparty já vêm mascarados da View vw_account_statement
    /// 
    /// **RN05 - Imutabilidade:**
    /// - Retorna snapshots congelados no momento da transação
    /// </remarks>
    Task<StatementResult> GetStatementAsync(
        StatementQuery query,
        CancellationToken cancellationToken = default);
}
