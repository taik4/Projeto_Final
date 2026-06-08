using BankingCore.Domain.Enums;
using FluentValidation;

namespace BankingCore.Application.DTOs;

/// <summary>
/// DTO para criação de conta bancária (POST /api/accounts).
/// </summary>
public record CreateAccountRequest(
    string HolderName,
    string HolderEmail,
    string HolderCpf
);

/// <summary>
/// Validator para CreateAccountRequest — validações na borda (CONSTITUTION Lei I.1).
/// </summary>
public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.HolderName)
            .NotEmpty().WithMessage("Nome do titular é obrigatório.")
            .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres.")
            .MaximumLength(120).WithMessage("Nome deve ter no máximo 120 caracteres.");

        RuleFor(x => x.HolderEmail)
            .NotEmpty().WithMessage("Email do titular é obrigatório.")
            .EmailAddress().WithMessage("Email inválido.")
            .MaximumLength(255);

        RuleFor(x => x.HolderCpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(BeValidCpf).WithMessage("CPF inválido.");
    }

    private static bool BeValidCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;
        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        if (digits.Length != 11) return false;
        if (digits.Distinct().Count() == 1) return false;

        var numbers = digits.Select(c => c - '0').ToArray();
        var sum1 = 0;
        for (var i = 0; i < 9; i++) sum1 += numbers[i] * (10 - i);
        var r1 = sum1 % 11;
        var d1 = r1 < 2 ? 0 : 11 - r1;
        if (numbers[9] != d1) return false;

        var sum2 = 0;
        for (var i = 0; i < 10; i++) sum2 += numbers[i] * (11 - i);
        var r2 = sum2 % 11;
        var d2 = r2 < 2 ? 0 : 11 - r2;
        return numbers[10] == d2;
    }
}

/// <summary>
/// DTO para atualização de status da conta (PUT /api/accounts/{id}/status).
/// </summary>
public record UpdateAccountStatusRequest(
    AccountStatus Status
);

/// <summary>
/// DTO para adicionar saldo à conta (POST /api/accounts/{id}/balance).
/// </summary>
public record AddBalanceRequest(
    decimal Amount
);

/// <summary>
/// Validator para UpdateAccountStatusRequest.
/// </summary>
public class UpdateAccountStatusRequestValidator : AbstractValidator<UpdateAccountStatusRequest>
{
    public UpdateAccountStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .NotEqual(AccountStatus.Closed).WithMessage("Para fechar a conta use DELETE /api/accounts/{id}.");
    }
}

/// <summary>
/// Response da conta. Campos sensíveis já vêm mascarados pela View do MySQL.
/// </summary>
public record AccountResponse(
    Guid AccountId,
    Guid? UserId,
    string HolderName,
    string HolderEmail,
    decimal Balance,
    AccountStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Conversor estático Account → AccountResponse para manter o controller magro.
/// </summary>
public static class AccountResponseMapper
{
    public static AccountResponse FromEntity(Domain.Entities.Account account) =>
        new(
            AccountId: account.AccountId,
            UserId: account.UserId,
            HolderName: account.HolderName,
            HolderEmail: account.HolderEmail,
            Balance: account.Balance,
            Status: (AccountStatus)(int)account.Status,
            CreatedAt: account.CreatedAt,
            UpdatedAt: account.UpdatedAt);
}
