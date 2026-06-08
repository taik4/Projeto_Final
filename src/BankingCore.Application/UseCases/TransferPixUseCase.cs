using System.Security.Cryptography;
using BankingCore.Application.DTOs;
using BankingCore.Application.Events;
using BankingCore.Domain.Enums;
using BankingCore.Domain.Exceptions;
using BankingCore.Domain.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace BankingCore.Application.UseCases;

/// <summary>
/// Use Case: Transferência PIX (RF03).
/// Orquestra validação, geração de E2E ID, chamada à SP (via Dapper) e publicação de evento.
///
/// CONSTITUTION Lei III.1: "Exceptions para infra, Result/ProblemDetails para negócio".
/// Aqui, erros de negócio da SP são retornados via PixTransferResponse (não lançam exceção).
/// </summary>
public sealed class TransferPixUseCase
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IValidator<PixTransferRequest> _validator;
    private readonly ILogger<TransferPixUseCase> _logger;

    public TransferPixUseCase(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        IEventPublisher eventPublisher,
        IValidator<PixTransferRequest> validator,
        ILogger<TransferPixUseCase> logger)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _eventPublisher = eventPublisher;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executa a transferência PIX. O <paramref name="senderUserId"/> é extraído do JWT
    /// e usado para autorizar o acesso à conta de origem (previne IDOR).
    /// </summary>
    public async Task<PixTransferResponse> ExecuteAsync(
        Guid senderUserId,
        PixTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validação na borda (FluentValidation)
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            throw new Domain.Exceptions.ValidationException(
                validation.Errors.Select(e => e.ErrorMessage));

        // 2. Autorização: usuário só pode usar sua própria conta como origem
        var sourceAccount = await _accountRepository.GetByUserIdAsync(senderUserId, cancellationToken)
            ?? throw new NotFoundException("Conta de origem do usuário autenticado");

        if (sourceAccount.Status != AccountStatus.Active)
            throw new DomainException("Sua conta não está ativa para realizar transferências.");

        // 3. Geração de EndToEndId (único por transferência PIX, padrão BACEN = 32 chars)
        var endToEndId = GenerateEndToEndId();

        // 4. IdempotencyKey (RN02) — se o client não enviou, geramos um
        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid();

        // 5. Chama a SP via repositório Dapper
        _logger.LogInformation(
            "Iniciando transferência PIX: Source={Source}, Target={Target}, Amount={Amount:C}, E2E={E2E}",
            sourceAccount.AccountId,
            request.TargetAccountId,
            request.Amount,
            endToEndId);

        var result = await _transactionRepository.ProcessTransferAsync(
            sourceAccountId: sourceAccount.AccountId,
            targetAccountId: request.TargetAccountId,
            amount: request.Amount,
            endToEndId: endToEndId,
            idempotencyKey: idempotencyKey,
            description: request.Description ?? string.Empty,
            receiverName: request.ReceiverName,
            receiverDocMasked: request.ReceiverDocMasked,
            cancellationToken);

        // 6. Se a transferência foi efetivada (ou foi idempotente), publica evento
        if (result.IsSuccess)
        {
            try
            {
                await _eventPublisher.PublishAsync(new PixTransferCompletedEvent
                {
                    EventId = Guid.NewGuid(),
                    EndToEndId = result.EndToEndId ?? endToEndId,
                    SourceAccountId = sourceAccount.AccountId,
                    TargetAccountId = request.TargetAccountId,
                    Amount = request.Amount,
                    Status = result.IsIdempotent ? "IDEMPOTENT" : "COMPLETED",
                    IdempotencyKey = idempotencyKey
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // Falha na publicação de evento não deve reverter a transferência bancária
                // (transferência já foi commitada no MySQL). Logamos e seguimos.
                _logger.LogWarning(ex, "[EVENT] Falha ao publicar evento PIX, mas transferência foi efetivada. E2E={E2E}", result.EndToEndId);
            }
        }

        // 7. Mapeia para o DTO de resposta
        var status = result.ResultCode switch
        {
            PixResultCode.Success => "SETTLED",
            PixResultCode.Idempotent => "DUPLICATE",
            PixResultCode.InsufficientBalance => "REJECTED_INSUFFICIENT_BALANCE",
            PixResultCode.AccountNotFound => "REJECTED_ACCOUNT_NOT_FOUND",
            PixResultCode.AccountInactive => "REJECTED_ACCOUNT_INACTIVE",
            PixResultCode.SameAccount => "REJECTED_SAME_ACCOUNT",
            PixResultCode.LockFailed => "REJECTED_LOCK_FAILED",
            _ => "REJECTED"
        };

        return new PixTransferResponse(
            Status: status,
            EndToEndId: result.EndToEndId ?? endToEndId,
            Message: result.Message,
            IsIdempotent: result.IsIdempotent);
    }

    /// <summary>
    /// Gera EndToEndId único seguindo formato do BACEN (32 chars alfanuméricos).
    /// Prefixo "E" + 31 chars aleatórios de base64-url-safe.
    /// </summary>
    private static string GenerateEndToEndId()
    {
        var buffer = new byte[24]; // 24 bytes → 32 chars em base64
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        var suffix = Convert.ToBase64String(buffer)
            .Replace('+', 'A')
            .Replace('/', 'B')
            .Replace('=', '0')
            .Substring(0, 31)
            .ToUpperInvariant();
        return "E" + suffix;
    }
}
