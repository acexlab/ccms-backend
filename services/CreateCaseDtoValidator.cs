using FluentValidation;
using ccms_backend.dtos;

namespace ccms_backend.services;

public class CreateCaseDtoValidator : AbstractValidator<CreateCaseDto>
{
    public CreateCaseDtoValidator()
    {
        RuleFor(x => x.ComplainantName)
            .NotEmpty().WithMessage("Complainant Name is required.")
            .MaximumLength(100).WithMessage("Complainant Name cannot exceed 100 characters.");

        RuleFor(x => x.ComplainantId)
            .NotEmpty().WithMessage("Complainant ID is required.")
            .Matches(@"^(?:\d{12}|[a-zA-Z]{5}[0-9]{4}[a-zA-Z]{1})$").WithMessage("Complainant ID must be a valid 12-digit Aadhaar or standard PAN format.");

        RuleFor(x => x.DefendantName)
            .NotEmpty().WithMessage("Defendant Name is required.")
            .MaximumLength(100).WithMessage("Defendant Name cannot exceed 100 characters.");

        RuleFor(x => x.DefendantId)
            .NotEmpty().WithMessage("Defendant ID is required.")
            .Matches(@"^(?:\d{12}|[a-zA-Z]{5}[0-9]{4}[a-zA-Z]{1})$").WithMessage("Defendant ID must be a valid 12-digit Aadhaar or standard PAN format.");

        RuleFor(x => x.DefendantAccountNumber)
            .NotEmpty().WithMessage("Defendant Account Number is required.")
            .Matches(@"^\d{9,18}$").WithMessage("Defendant Account Number must be between 9 and 18 digits.");

        RuleFor(x => x.DefendantBankName)
            .NotEmpty().WithMessage("Defendant Bank Name is required.");

        RuleFor(x => x.OrderType)
            .NotEmpty().WithMessage("Order Type is required.")
            .Must(x => x == "FreezeAccount" || x == "BalanceEnquiry").WithMessage("Order Type must be either 'FreezeAccount' or 'BalanceEnquiry'.");

        RuleFor(x => x.FreezeAmount)
            .GreaterThan(0).WithMessage("Freeze Amount must be greater than zero.")
            .When(x => x.OrderType == "FreezeAccount");
    }
}