namespace BankingCore.Application.DTOs;

/// <summary>
/// DTO para requisição de consulta de extrato bancário.
/// Implementa paginação por cursor (Keyset) para performance em grandes volumes de dados.
/// </summary>
/// <remarks>
/// **Paginação por Cursor vs Offset:**
/// - Cursor é o último `transaction_id` retornado na página anterior
/// - Evita problemas de performance com OFFSET em tabelas grandes
/// - Mantém consistência mesmo com inserções simultâneas
/// </remarks>
public readonly record struct StatementRequest(
    Guid AccountId,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Cursor = null,
    int? Limit = 50
);

/// <summary>
/// DTO para resposta de extrato bancário com paginação.
/// </summary>
/// <param name="Transactions">Lista de transações na página atual</param>
/// <param name="NextCursor">Cursor para a próxima página (null se for a última)</param>
/// <param name="HasMore">Indica se existem mais transações disponíveis</param>
public readonly record struct StatementResponse(
    IReadOnlyList<TransactionDto> Transactions,
    string? NextCursor,
    bool HasMore
);

/// <summary>
/// DTO representando uma transação no extrato bancário.
/// </summary>
/// <remarks>
/// **RN04 - Transparência PIX:**
/// - `EndToEndId` sempre retornado para rastreamento
/// - `CounterpartyName` e `CounterpartyDocument` já vêm mascarados da View
/// 
/// **RN05 - Imutabilidade:**
/// - Campos de recebedor são snapshots do momento da transação
/// - Não refletem alterações posteriores nos dados do usuário
/// </remarks>
public readonly record struct TransactionDto(
    long TransactionId,
    string EndToEndId,
    DateTime Date,
    TransactionType Type,
    string? Description,
    decimal Amount,
    string Status,
    string CounterpartyName,
    string CounterpartyDocument
);

/// <summary>
/// Tipo de transação no extrato (entrada ou saída).
/// </summary>
public enum TransactionType
{
    /// <summary>Dinheiro recebido (crédito na conta)</summary>
    Credit,
    
    /// <summary>Dinheiro enviado (débito na conta)</summary>
    Debit
}
