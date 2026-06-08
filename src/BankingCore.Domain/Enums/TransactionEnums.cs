namespace BankingCore.Domain.Enums;

/// <summary>
/// Status de uma transação PIX no banco.
/// Mapeado para ENUM('PENDING','COMPLETED','REVERTED','FAILED') do MySQL.
/// </summary>
public enum TransactionStatus
{
    Pending = 0,
    Completed = 1,
    Reverted = 2,
    Failed = 3
}

/// <summary>
/// Direção da transação — débito na origem, crédito no destino.
/// </summary>
public enum TransactionDirection
{
    Debit = 0,
    Credit = 1
}

/// <summary>
/// Código de resultado retornado pela SP sp_process_pix_transfer.
/// Mantido separado para mapear códigos do banco para tipos do .NET.
/// </summary>
public enum PixResultCode
{
    /// <summary>Transferência processada com sucesso.</summary>
    Success = 0,
    /// <summary>Idempotente — transfer já havia sido processada.</summary>
    Idempotent = 1,
    /// <summary>Saldo insuficiente na conta de origem.</summary>
    InsufficientBalance = 2,
    /// <summary>Não foi possível obter lock na conta de origem (NOWAIT).</summary>
    LockFailed = 3,
    /// <summary>Conta não encontrada.</summary>
    AccountNotFound = 4,
    /// <summary>Conta inativa (BLOCKED ou CLOSED).</summary>
    AccountInactive = 5,
    /// <summary>Conta de origem e destino não podem ser iguais.</summary>
    SameAccount = 6,
    /// <summary>Erro interno (SQL exception genérica).</summary>
    InternalError = 99
}
