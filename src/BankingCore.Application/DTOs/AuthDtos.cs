using FluentValidation;

namespace BankingCore.Application.DTOs;

/// <summary>
/// DTO para registro de novo usuário (POST /api/auth/register).
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string Cpf,
    string HolderName
);

/// <summary>
/// Validator para RegisterRequest.
/// Validações na borda (CONSTITUTION Lei I.1).
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório.")
            .EmailAddress().WithMessage("Email inválido.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.")
            .Matches("[A-Z]").WithMessage("Senha deve conter ao menos uma letra maiúscula.")
            .Matches("[a-z]").WithMessage("Senha deve conter ao menos uma letra minúscula.")
            .Matches("[0-9]").WithMessage("Senha deve conter ao menos um número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Senha deve conter ao menos um caractere especial.");

        RuleFor(x => x.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(IsValidCpf).WithMessage("CPF inválido.");

        RuleFor(x => x.HolderName)
            .NotEmpty().WithMessage("Nome do titular é obrigatório.")
            .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres.")
            .MaximumLength(120);
    }

    private static bool IsValidCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;

        // Remove caracteres não numéricos
        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        if (digits.Length != 11) return false;

        // Rejeita CPFs com todos os dígitos iguais
        if (digits.Distinct().Count() == 1) return false;

        // Validação do CPF (algoritmo do dígito verificador brasileiro)
        var numbers = digits.Select(c => c - '0').ToArray();
        var sum1 = 0;
        for (var i = 0; i < 9; i++) sum1 += numbers[i] * (10 - i);
        var remainder1 = sum1 % 11;
        var checkDigit1 = remainder1 < 2 ? 0 : 11 - remainder1;
        if (numbers[9] != checkDigit1) return false;

        var sum2 = 0;
        for (var i = 0; i < 10; i++) sum2 += numbers[i] * (11 - i);
        var remainder2 = sum2 % 11;
        var checkDigit2 = remainder2 < 2 ? 0 : 11 - remainder2;
        return numbers[10] == checkDigit2;
    }
}

/// <summary>
/// DTO para login de usuário (POST /api/auth/login).
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Validator para LoginRequest.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório.")
            .EmailAddress().WithMessage("Email inválido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.");
    }
}

/// <summary>
/// Response de autenticação retornada por login e register.
/// (RF01: Cadastro e Login com emissão de JWT)
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);
