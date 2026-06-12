/*
 * File: CaseService.cs
 * Description: Core business logic service handling case creations, details retrieval, file uploads, and responses.
 * To Implement: Keep in sync with EF repositories and exception handling configurations.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;

namespace ccms_backend.services;

public class CaseService
{
    private readonly ICaseRepository _caseRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly CaseNumberGenerator _caseNumberGenerator;
    private readonly IValidator<CreateCaseDto> _createValidator;
    private readonly IValidator<SubmitResponseDto> _responseValidator;

    public CaseService(
        ICaseRepository caseRepository,
        IFileStorageService fileStorageService,
        CaseNumberGenerator caseNumberGenerator,
        IValidator<CreateCaseDto> createValidator,
        IValidator<SubmitResponseDto> responseValidator)
    {
        _caseRepository = caseRepository;
        _fileStorageService = fileStorageService;
        _caseNumberGenerator = caseNumberGenerator;
        _createValidator = createValidator;
        _responseValidator = responseValidator;
    }

    public async Task<CaseSummaryDto> CreateCaseAsync(
        CreateCaseDto dto,
        Stream courtOrder, string courtOrderName,
        Stream aadhaar, string aadhaarName,
        Stream pan, string panName,
        int createdByUserId,
        CancellationToken ct = default)
    {
        var validationResult = await _createValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Generate Case Number
        var caseNumber = await _caseNumberGenerator.GenerateAsync(ct);

        // Upload documents
        var orderPath = await _fileStorageService.SaveFileAsync(courtOrder, courtOrderName, "court-orders", ct);
        var aadhaarPath = await _fileStorageService.SaveFileAsync(aadhaar, aadhaarName, "aadhaar-copies", ct);
        var panPath = await _fileStorageService.SaveFileAsync(pan, panName, "pan-copies", ct);

        // Map order type
        var parsedOrderType = Enum.Parse<OrderType>(dto.OrderType);

        var @case = new Case
        {
            CaseNumber = caseNumber,
            OrderType = parsedOrderType,
            Status = CaseStatus.Pending,
            ComplainantName = dto.ComplainantName,
            DefendantName = dto.DefendantName,
            DefendantAadhaar = dto.DefendantAadhaar,
            DefendantPan = dto.DefendantPan,
            DefendantAccountNumber = dto.DefendantAccountNumber,
            BankCode = dto.BankCode,
            FreezeAmount = parsedOrderType == OrderType.FreezeAccount ? dto.FreezeAmount : null,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        @case.Documents.Add(new CaseDocument
        {
            DocumentType = DocumentType.CourtOrder,
            FileName = courtOrderName,
            FilePath = orderPath
        });

        @case.Documents.Add(new CaseDocument
        {
            DocumentType = DocumentType.AadhaarCopy,
            FileName = aadhaarName,
            FilePath = aadhaarPath
        });

        @case.Documents.Add(new CaseDocument
        {
            DocumentType = DocumentType.PanCopy,
            FileName = panName,
            FilePath = panPath
        });

        await _caseRepository.AddAsync(@case, ct);

        return new CaseSummaryDto
        {
            Id = @case.Id,
            CaseNumber = @case.CaseNumber,
            OrderType = @case.OrderType.ToString(),
            Status = @case.Status.ToString(),
            DefendantName = @case.DefendantName,
            CreatedAt = @case.CreatedAt
        };
    }

    public async Task<List<CaseSummaryDto>> GetMyCasesAsync(int userId, CancellationToken ct = default)
    {
        var cases = await _caseRepository.GetByUserIdAsync(userId, ct);
        return cases.Select(c => new CaseSummaryDto
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            OrderType = c.OrderType.ToString(),
            Status = c.Status.ToString(),
            DefendantName = c.DefendantName,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<List<CaseSummaryDto>> GetCasesForBankAsync(string bankCode, CancellationToken ct = default)
    {
        var cases = await _caseRepository.GetByBankCodeAsync(bankCode, ct);
        return cases.Select(c => new CaseSummaryDto
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            OrderType = c.OrderType.ToString(),
            Status = c.Status.ToString(),
            DefendantName = c.DefendantName,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<CaseDetailDto> GetCaseByIdAsync(
        int id,
        int requestingUserId,
        string requestingUserRole,
        string? requestingUserBankCode,
        CancellationToken ct = default)
    {
        var @case = await _caseRepository.GetByIdAsync(id, ct);
        if (@case == null)
        {
            throw new CaseNotFoundException(id);
        }

        // Authorization Checks
        if (requestingUserRole == UserRole.CourtOfficer.ToString())
        {
            if (@case.CreatedByUserId != requestingUserId)
            {
                throw new UnauthorisedActionException("You are not authorized to view this case.");
            }
        }
        else if (requestingUserRole == UserRole.BankOfficer.ToString())
        {
            if (@case.BankCode != requestingUserBankCode)
            {
                throw new UnauthorisedActionException("You are not authorized to view cases for this bank.");
            }
        }
        else
        {
            throw new UnauthorisedActionException("Access denied.");
        }

        var dto = new CaseDetailDto
        {
            Id = @case.Id,
            CaseNumber = @case.CaseNumber,
            OrderType = @case.OrderType.ToString(),
            Status = @case.Status.ToString(),
            ComplainantName = @case.ComplainantName,
            DefendantName = @case.DefendantName,
            DefendantAadhaar = DataMaskingHelper.MaskAadhaar(@case.DefendantAadhaar),
            DefendantPan = DataMaskingHelper.MaskPan(@case.DefendantPan),
            DefendantAccountNumber = DataMaskingHelper.MaskAccountNumber(@case.DefendantAccountNumber),
            BankCode = @case.BankCode,
            FreezeAmount = @case.FreezeAmount,
            MatchedAccountNumber = @case.MatchedAccountNumber != null ? DataMaskingHelper.MaskAccountNumber(@case.MatchedAccountNumber) : null,
            MatchedBalance = @case.MatchedBalance,
            MatchedAccountStatus = @case.MatchedAccountStatus,
            CreatedByUserId = @case.CreatedByUserId,
            CreatedAt = @case.CreatedAt,
            Documents = @case.Documents.Select(d => new CaseDocumentDto
            {
                Id = d.Id,
                DocumentType = d.DocumentType.ToString(),
                FileName = d.FileName,
                FileUrl = _fileStorageService.GetFileUrl(d.FilePath),
                UploadedAt = d.UploadedAt
            }).ToList()
        };

        if (@case.Response != null)
        {
            dto.Response = new CaseResponseDto
            {
                ResponseType = @case.Response.ResponseType,
                ReportedAmount = @case.Response.ReportedAmount,
                Remarks = @case.Response.Remarks,
                IsSystemGenerated = @case.Response.IsSystemGenerated,
                RespondedAt = @case.Response.RespondedAt
            };
        }

        return dto;
    }

    public async Task SubmitResponseAsync(int caseId, SubmitResponseDto dto, int bankOfficerUserId, CancellationToken ct = default)
    {
        var validationResult = await _responseValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var @case = await _caseRepository.GetByIdAsync(caseId, ct);
        if (@case == null)
        {
            throw new CaseNotFoundException(caseId);
        }

        if (@case.Status != CaseStatus.AccountValidated)
        {
            throw new InvalidOperationException("Responses can only be submitted for validated cases.");
        }

        if (@case.Response != null)
        {
            throw new CaseAlreadyRespondedException(@case.CaseNumber);
        }

        // Additional manual check for reported amount on freeze orders
        if (@case.OrderType == OrderType.FreezeAccount && (dto.ReportedAmount == null || dto.ReportedAmount <= 0))
        {
            throw new ValidationException("Reported Freeze Amount is required and must be greater than zero for FreezeAccount orders.");
        }

        var responseType = @case.OrderType == OrderType.FreezeAccount ? "FreezeApplied" : "BalanceProvided";

        var response = new CaseResponse
        {
            ResponseType = responseType,
            ReportedAmount = dto.ReportedAmount ?? @case.MatchedBalance,
            Remarks = dto.Remarks,
            IsSystemGenerated = false,
            RespondedByUserId = bankOfficerUserId,
            RespondedAt = DateTime.UtcNow
        };

        @case.Response = response;
        @case.Status = @case.OrderType == OrderType.FreezeAccount ? CaseStatus.FreezeApplied : CaseStatus.BalanceProvided;

        await _caseRepository.UpdateAsync(@case, ct);
    }
}
