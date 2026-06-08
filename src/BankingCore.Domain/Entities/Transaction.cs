using BankingCore.Domain.Enums;

namespace BankingCore.Domain.Entities;

/// <summary>
/// Entidade Transaction — representa uma linha da tabela `transactions`.
/// Mapeada 1:1 com a tabela criada em db/init.sql.
///
/// Design decisions:
///   • Immutable — transações PIX são imutáveis após commit (RN05).
///   • Direction: DEBIT (saída) ou CREDIT (entrada) na mesma tabela.
///   • receiver_name_snapshot / receiver_doc_snapshot: dados congelados no momento da transação.
/// </summary>
public class Transaction
{
    public long TransactionId { get; private set; }
    public string EndToEndId { get; private set; } = string.Empty;
    public Guid? IdempotencyKey { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid TargetAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionDirection Direction { get; private set; }
    public string? Description { get; private set; }
    public string ReceiverNameSnapshot { get; private set; } = string.Empty;
    public string ReceiverDocSnapshot { get; private set; } = string.Empty;
    public TransactionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Account? SourceAccount { get; private set; }
    public Account? TargetAccount { get; private set; }

    private Transaction() { } // EF Core / Dapper
}
