using FluentValidation;

namespace BankingCore.Application.DTOs;

/// <summary>
/// Request para transferência PIX (POST /api/pix/transfer).
/// </summary>
/// <param name="TargetAccountId">UUID da conta de destino (recebedor).</param>
/// <param name="Amount">Valor em BRL. Deve ser > 0.</param>
/// <param name="Description">Mensagem opcional (máx 255 chars).</param>
/// <param name="ReceiverName">Nome completo do recebedor (snapshot - RN05).</param>
/// <param name="ReceiverDocMasked">Documento mascarado do recebedor (RN04).</param>
/// <param name="IdempotencyKey">UUID para garantir idempotência (RN02). Se nulo, será gerado pelo servidor.</param>
public record PixTransferRequest(
    Guid TargetAccountId,
    decimal Amount,
    string? Description,
    string ReceiverName,
    string ReceiverDocMasked,
    Guid? IdempotencyKey
);

/// <summary>
/// Response de transferência PIX (retorno da SP).
/// </summary>
public record PixTransferResponse(
    string Status,
    string EndToEndId,
    string Message,
    bool IsIdempotent
);

/// <summary>
/// Validator para PixTransferRequest. Validações na borda (CONSTITUTION Lei I.1).
/// </summary>
public sealed class PixTransferRequestValidator : AbstractValidator<PixTransferRequest>
{
    public PixTransferRequestValidator()
    {
        RuleFor(x => x.TargetAccountId)
            .NotEmpty().WithMessage("Conta de destino é obrigatória.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("O valor da transferência deve ser maior que zero.")
            .LessThanOrEqualTo(1_000_000m).WithMessage("Valor máximo por transferência: R$ 1.000.000,00.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Descrição deve ter no máximo 255 caracteres.");

        RuleFor(x => x.ReceiverName)
            .NotEmpty().WithMessage("Nome do recebedor é obrigatório.")
            .MaximumLength(120).WithMessage("Nome do recebedor deve ter no máximo 120 caracteres.");

        RuleFor(x => x.ReceiverDocMasked)
            .NotEmpty().WithMessage("Documento mascarado do recebedor é obrigatório.")
            .MaximumLength(14).WithMessage("Documento mascarado deve ter no máximo 14 caracteres.");
    }
}
