using FluentValidation;
using ccms_backend.dtos;

namespace ccms_backend.services;

public class SubmitResponseDtoValidator : AbstractValidator<SubmitResponseDto>
{
    public SubmitResponseDtoValidator()
    {
        RuleFor(x => x.ResponseType)
            .NotEmpty().WithMessage("Response Type is required.")
            .Must(x => x == "FreezeApplied" || x == "BalanceProvided" || x == "AccountNotFound")
            .WithMessage("Response Type must be 'FreezeApplied', 'BalanceProvided', or 'AccountNotFound'.");

        RuleFor(x => x.FreezeAmountApplied)
            .NotNull().WithMessage("Freeze Amount Applied is required for Freeze responses.")
            .GreaterThan(0).WithMessage("Freeze Amount Applied must be greater than zero.")
            .When(x => x.ResponseType == "FreezeApplied");

        RuleFor(x => x.BalanceReported)
            .NotNull().WithMessage("Balance Reported is required for Balance Provided responses.")
            .GreaterThanOrEqualTo(0).WithMessage("Balance Reported must be non-negative.")
            .When(x => x.ResponseType == "BalanceProvided");

        RuleFor(x => x.Remarks)
            .NotEmpty().WithMessage("Remarks are required.")
            .MaximumLength(500).WithMessage("Remarks cannot exceed 500 characters.");
    }
}