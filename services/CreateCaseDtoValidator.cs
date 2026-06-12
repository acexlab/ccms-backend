/*
 * File: CreateCaseDtoValidator.cs
 * Description: FluentValidation rules for creating a Case.
 * To Implement: Validation checks for Aadhaar (12 digits), PAN format, and conditional FreezeAmount.
 */

using FluentValidation;
using ccms_backend.dtos;

namespace ccms_backend.services;

public class CreateCaseDtoValidator : AbstractValidator<CreateCaseDto>
{
    public CreateCaseDtoValidator()
    {
        RuleFor(x => x.ComplainantName)
            .NotEmpty().WithMessage("Complainant Name is required.");

        RuleFor(x => x.DefendantName)
            .NotEmpty().WithMessage("Defendant Name is required.");

        RuleFor(x => x.DefendantAadhaar)
            .NotEmpty().WithMessage("Aadhaar Number is required.")
            .Matches(@"^\d{12}$").WithMessage("Aadhaar Number must be exactly 12 digits.");

        RuleFor(x => x.DefendantPan)
            .NotEmpty().WithMessage("PAN is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$").WithMessage("PAN must be in format ABCDE1234F.");

        RuleFor(x => x.DefendantAccountNumber)
            .NotEmpty().WithMessage("Account Number is required.");

        RuleFor(x => x.BankCode)
            .NotEmpty().WithMessage("Bank Code is required.");

        RuleFor(x => x.OrderType)
            .NotEmpty().WithMessage("Order Type is required.")
            .Must(x => x == "FreezeAccount" || x == "BalanceEnquiry")
            .WithMessage("Order Type must be FreezeAccount or BalanceEnquiry.");

        RuleFor(x => x.FreezeAmount)
            .NotNull().When(x => x.OrderType == "FreezeAccount")
            .WithMessage("Freeze Amount is required when Order Type is FreezeAccount.")
            .GreaterThan(0).When(x => x.OrderType == "FreezeAccount")
            .WithMessage("Freeze Amount must be greater than zero.");
    }
}
