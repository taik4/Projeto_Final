namespace BankingCore.Domain.DTOs;

/// <summary>
/// Parâmetros de consulta de extrato bancário.
/// Contrato de entrada do repositório de transações.
/// </summary>
public readonly record struct StatementQuery(
    Guid AccountId,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Cursor = null,
    int? Limit = 50
);

/// <summary>
/// Resultado de consulta de extrato bancário com paginação.
/// Contrato de saída do repositório de transações.
/// </summary>
public readonly record struct StatementResult(
    IReadOnlyList<TransactionInfo> Transactions,
    string? NextCursor,
    bool HasMore
);

/// <summary>
/// Informação de uma transação retornada pelo repositório.
/// </summary>
public readonly record struct TransactionInfo(
    long TransactionId,
    string EndToEndId,
    DateTime Date,
    string Direction,
    string? Description,
    decimal Amount,
    string Status,
    string CounterpartyName,
    string CounterpartyDocument
);
