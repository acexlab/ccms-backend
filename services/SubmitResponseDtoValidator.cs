/*
 * File: SubmitResponseDtoValidator.cs
 * Description: FluentValidation rules for submitting a response to a case.
 * To Implement: Validation checks for Remarks, and conditional check for ReportedAmount on FreezeAccount orders.
 */

using FluentValidation;
using ccms_backend.dtos;

namespace ccms_backend.services;

public class SubmitResponseDtoValidator : AbstractValidator<SubmitResponseDto>
{
    public SubmitResponseDtoValidator()
    {
        RuleFor(x => x.Remarks)
            .NotEmpty().WithMessage("Remarks are required.");
    }
}
