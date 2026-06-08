using BankingCore.Application.DTOs;
using FluentValidation;

namespace BankingCore.Application.Validators;

public class StatementRequestValidator : AbstractValidator<StatementRequest>
{
    public StatementRequestValidator()
    {
        RuleFor(x => x.Limit)
            .LessThanOrEqualTo(100)
            .WithMessage("Limite máximo de 100 transações por página")
            .GreaterThan(0)
            .WithMessage("Limite deve ser maior que zero")
            .When(x => x.Limit.HasValue);

        When(x => x.StartDate.HasValue, () =>
        {
            RuleFor(x => x.StartDate)
                .Must(d => d.Value.Date <= DateTime.UtcNow.Date)
                .WithMessage("Data início não pode ser futura");

            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("Data fim deve ser maior ou igual à data início")
                .When(x => x.EndDate.HasValue);
        });

        When(x => !string.IsNullOrEmpty(x.Cursor), () =>
        {
            RuleFor(x => x.Cursor)
                .Must(c => long.TryParse(c, out _))
                .WithMessage("Cursor deve ser um número válido");
        });
    }
}

