using Microsoft.Extensions.Logging;

namespace BankingCore.Application.Events;

/// <summary>
/// Implementação fallback de IEventPublisher que apenas loga eventos no ILogger.
///
/// Usada em desenvolvimento e como fallback de segurança em produção caso o Kafka
/// esteja indisponível (CONSTITUTION PLAN §5: "InMemoryPublisher loga no console
/// caso o container do Kafka falhe, garantindo que a API não trave").
///
/// Em produção, substituir por KafkaEventPublisher que escreve em tópico Kafka real.
/// </summary>
public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync(PixTransferCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        // Simula latência de rede para um broker real (~10ms)
        await Task.Delay(10, cancellationToken);

        _logger.LogInformation(
            "📨 [PIX-EVENT] Evento publicado: EventId={EventId}, EndToEndId={EndToEndId}, " +
            "Source={SourceAccountId}, Target={TargetAccountId}, Amount={Amount:C}, Status={Status}",
            @event.EventId,
            @event.EndToEndId,
            @event.SourceAccountId,
            @event.TargetAccountId,
            @event.Amount,
            @event.Status);
    }
}
