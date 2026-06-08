namespace BankingCore.Domain.Enums;

/// <summary>
/// Status do ciclo de vida de uma conta bancária.
/// Mapeado para ENUM('ACTIVE','BLOCKED','CLOSED') no MySQL.
/// </summary>
public enum AccountStatus
{
    Active = 0,
    Blocked = 1,
    Closed = 2
}
