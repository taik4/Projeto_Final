namespace BankingCore.Application.Events;

/// <summary>
/// Evento publicado quando uma transferência PIX é concluída com sucesso.
/// Consumido por assinantes externos (Kafka, filas, dashboards, auditoria).
/// </summary>
public sealed class PixTransferCompletedEvent
{
    /// <summary>ID único do evento (UUID v4).</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>Timestamp UTC em que o evento foi publicado.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>EndToEndId da transação PIX (único por transferência).</summary>
    public string EndToEndId { get; init; } = string.Empty;

    /// <summary>UUID da conta de origem.</summary>
    public Guid SourceAccountId { get; init; }

    /// <summary>UUID da conta de destino.</summary>
    public Guid TargetAccountId { get; init; }

    /// <summary>Valor transferido em BRL.</summary>
    public decimal Amount { get; init; }

    /// <summary>Status do resultado (COMPLETED ou IDEMPOTENT).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>ID da chave de idempotência usada.</summary>
    public Guid IdempotencyKey { get; init; }
}

/// <summary>
/// Contrato para publicação de eventos do domínio.
/// Implementações: InMemoryEventPublisher (dev) e KafkaEventPublisher (prod).
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publica um evento de transferência PIX concluída.
    /// Implementações devem ser não-bloqueantes e tolerantes a falhas de infraestrutura.
    /// CONSTITUTION Lei PLAN §5 — fallback InMemory garante que a transação seja salva
    /// mesmo se o broker de mensagens estiver offline.
    /// </summary>
    Task PublishAsync(PixTransferCompletedEvent @event, CancellationToken cancellationToken = default);
}
