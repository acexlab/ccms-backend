using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CCMS.Application.Interfaces;
using CCMS.Application.DTOs;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Application.Services;

public class CaseService
{
    private readonly IAppDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public CaseService(
        IAppDbContext context,
        IBlobStorageService blobStorage)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    public async Task<(string CaseNumber, int Id)> CreateCaseAsync(
        CreateCaseDto dto,
        Stream courtOrderStream,
        string courtOrderFileName,
        Stream aadhaarStream,
        string aadhaarFileName,
        string aadhaarContentType,
        Stream panStream,
        string panFileName,
        string panContentType,
        string username)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        await _context.BeginTransactionAsync();
        try
        {
            // Generate Case Number: CCMS-yyyyMMdd-XXXX
            var today = DateTime.UtcNow.Date;
            var count = await _context.Cases.CountAsync(c => c.CreatedAt >= today);
            int nextSequence = count + 1;
            var caseNumber = $"CCMS-{DateTime.UtcNow:yyyyMMdd}-{nextSequence:D4}";

            if (!Enum.TryParse<OrderType>(dto.OrderType, true, out var parsedOrderType))
            {
                parsedOrderType = OrderType.FreezeAccount;
            }

            var @case = new Case
            {
                CaseNumber = caseNumber,
                CreatedByUserId = user.Id,
                OrderType = parsedOrderType,
                FreezeAmount = parsedOrderType == OrderType.FreezeAccount ? dto.FreezeAmount : null,
                Status = CaseStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Cases.Add(@case);
            await _context.SaveChangesAsync();

            var complainant = new Complainant
            {
                CaseId = @case.Id,
                FullName = dto.ComplainantName,
                IdentityNumber = dto.ComplainantId
            };
            _context.Complainants.Add(complainant);
            // Link navigation property
            @case.Complainant = complainant;

            var defendant = new Defendant
            {
                CaseId = @case.Id,
                FullName = dto.DefendantName,
                IdentityNumber = dto.DefendantId,
                PanNumber = dto.DefendantPan,
                BankAccountNumber = dto.DefendantAccountNumber,
                BankName = dto.DefendantBankName
            };
            _context.Defendants.Add(defendant);
            // Link navigation property so EF Core resolves Include(c => c.Defendant) correctly
            @case.Defendant = defendant;

            // Save Court Order Copy File
            var uniqueCourtOrderName = $"{Guid.NewGuid()}_{courtOrderFileName}";
            await _blobStorage.UploadFileAsync(courtOrderStream, uniqueCourtOrderName, "application/pdf");

            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.CourtOrder,
                FileName = courtOrderFileName,
                FilePath = uniqueCourtOrderName,
                FileSize = (int)courtOrderStream.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save Aadhaar Copy File
            var uniqueAadhaarName = $"{Guid.NewGuid()}_{aadhaarFileName}";
            await _blobStorage.UploadFileAsync(aadhaarStream, uniqueAadhaarName, aadhaarContentType);
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.AadhaarCopy,
                FileName = aadhaarFileName,
                FilePath = uniqueAadhaarName,
                FileSize = (int)aadhaarStream.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save PAN Copy File
            var uniquePanName = $"{Guid.NewGuid()}_{panFileName}";
            await _blobStorage.UploadFileAsync(panStream, uniquePanName, panContentType);
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.PANCopy,
                FileName = panFileName,
                FilePath = uniquePanName,
                FileSize = (int)panStream.Length,
                UploadedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await _context.CommitTransactionAsync();

            return (caseNumber, @case.Id);
        }
        catch
        {
            await _context.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<CaseInboxDto> GetInboxAsync()
    {
        var cases = await _context.Cases
            .Include(c => c.Defendant)
            .Include(c => c.ValidationResult)
            .ToListAsync();

        var summaries = cases.Select(c => new CaseSummaryDto
        {
            CaseNumber = c.CaseNumber,
            DefendantName = c.Defendant?.FullName ?? string.Empty,
            OrderType = c.OrderType.ToString(),
            Status = c.Status.ToString(),
            ValidationDate = c.ValidationResult?.ValidatedAt
        }).ToList();

        var inbox = new CaseInboxDto
        {
            AwaitingAction = summaries.Where(c => c.Status == CaseStatus.AccountValidated.ToString() || c.Status == CaseStatus.UnderReview.ToString()).ToList(),
            PendingBatch = summaries.Where(c => c.Status == CaseStatus.Pending.ToString()).ToList(),
            Completed = summaries.Where(c => c.Status == CaseStatus.FreezeApplied.ToString() || c.Status == CaseStatus.BalanceProvided.ToString()).ToList(),
            AutoResolved = summaries.Where(c => c.Status == CaseStatus.AccountNotFound.ToString()).ToList()
        };

        return inbox;
    }

    public async Task<CaseDetailDto> GetCaseDetailAsync(string caseNumber, string userRole = "")
    {
        var caseEntity = await _context.Cases
            .Include(c => c.Complainant)
            .Include(c => c.Defendant)
            .Include(c => c.ValidationResult)
            .Include(c => c.Documents)
            .Include(c => c.Response)
            .FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);

        if (caseEntity == null)
        {
            throw new KeyNotFoundException("Case not found");
        }

        if (caseEntity.Status == CaseStatus.AccountValidated && userRole.Equals("Bank", StringComparison.OrdinalIgnoreCase))
        {
            caseEntity.Status = CaseStatus.UnderReview;
            caseEntity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var aadhaarPart = "";
        var panPart = "";

        if (caseEntity.Defendant != null && !string.IsNullOrEmpty(caseEntity.Defendant.PanNumber))
        {
            aadhaarPart = DataMaskingHelper.MaskIdentity(caseEntity.Defendant.IdentityNumber);
            panPart = DataMaskingHelper.MaskIdentity(caseEntity.Defendant.PanNumber);
        }
        else
        {
            var maskedIdentity = DataMaskingHelper.MaskIdentity(caseEntity.Defendant?.IdentityNumber) ?? "";
            aadhaarPart = maskedIdentity;
            if (maskedIdentity.Contains("PAN:")) {
                var parts = maskedIdentity.Split(new[] { ", PAN:" }, StringSplitOptions.None);
                aadhaarPart = parts[0].Replace("Aadhaar:", "").Trim();
                if (parts.Length > 1) panPart = parts[1].Trim();
            } else {
                aadhaarPart = aadhaarPart.Replace("Aadhaar:", "").Trim();
            }
        }

        var dto = new CaseDetailDto
        {
            CaseNumber = caseEntity.CaseNumber,
            OrderType = caseEntity.OrderType.ToString(),
            Status = caseEntity.Status.ToString(),
            ComplainantName = caseEntity.Complainant?.FullName ?? string.Empty,
            ComplainantIdentityNumber = caseEntity.Complainant?.IdentityNumber ?? string.Empty,
            DefendantName = caseEntity.Defendant?.FullName ?? string.Empty,
            DefendantAadhaar = aadhaarPart,
            DefendantPan = panPart,
            DefendantAccountNumber = DataMaskingHelper.MaskAccount(caseEntity.Defendant?.BankAccountNumber),

            MatchedAccountNumber = caseEntity.ValidationResult?.MatchedAccountNumber,
            AccountStatus = caseEntity.ValidationResult?.AccountStatus,
            CurrentBalance = caseEntity.ValidationResult?.CurrentBalance,
            ValidationTimestamp = caseEntity.ValidationResult?.ValidatedAt,

            Documents = caseEntity.Documents.Select(d => new CaseDocumentDto
            {
                Id = d.Id,
                DocumentType = d.DocumentType.ToString(),
                FileName = d.FileName,
                FileSize = d.FileSize,
                DownloadUrl = $"/api/cases/{caseEntity.CaseNumber}/documents/{d.Id}/download"
            }).ToList()
        };

        if (caseEntity.Response != null)
        {
            dto.Response = new CaseResponseDto
            {
                ResponseType = caseEntity.Response.ResponseType.ToString(),
                FreezeAmountApplied = caseEntity.Response.FreezeAmountApplied,
                BalanceReported = caseEntity.Response.BalanceReported,
                Remarks = caseEntity.Response.Remarks,
                SubmittedAt = caseEntity.Response.SubmittedAt
            };
        }

        return dto;
    }

    public async Task SubmitResponseAsync(string caseNumber, SubmitResponseDto payload, int? userId)
    {
        var caseEntity = await _context.Cases
            .Include(c => c.Response)
            .FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);

        if (caseEntity == null)
            throw new KeyNotFoundException("Case not found");

        if (caseEntity.Response != null)
            throw new InvalidOperationException("A response already exists for this case.");

        if (caseEntity.Status != CaseStatus.AccountValidated && caseEntity.Status != CaseStatus.UnderReview)
            throw new InvalidOperationException("Case is not in a status that allows response submission.");

        await _context.BeginTransactionAsync();
        try
        {
            var newStatus = payload.ResponseType == "FreezeApplied" ? CaseStatus.FreezeApplied :
                            payload.ResponseType == "BalanceProvided" ? CaseStatus.BalanceProvided :
                            payload.ResponseType == "AccountNotFound" ? CaseStatus.AccountNotFound : CaseStatus.Pending;

            caseEntity.Status = newStatus;
            caseEntity.UpdatedAt = DateTime.UtcNow;
            _context.Cases.Update(caseEntity);

            var responseEntity = new CaseResponse
            {
                CaseId = caseEntity.Id,
                ResponseType = Enum.Parse<ResponseType>(payload.ResponseType),
                FreezeAmountApplied = payload.FreezeAmountApplied,
                BalanceReported = payload.BalanceReported,
                Remarks = payload.Remarks,
                SubmittedAt = DateTime.UtcNow,
                RespondedByUserId = userId
            };

            _context.CaseResponses.Add(responseEntity);
            // CommitTransactionAsync already calls SaveChangesAsync internally.
            // Do NOT call SaveChangesAsync here — double-save causes EF to throw,
            // which triggers the catch block and rolls back the entire transaction.
            await _context.CommitTransactionAsync();
        }
        catch
        {
            await _context.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IEnumerable<CaseSummaryDto>> GetCasesAsync(string username)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) throw new UnauthorizedAccessException("User not found");

        if (user.Role == UserRole.Court)
        {
            var cases = await _context.Cases
                .Where(c => c.CreatedByUserId == user.Id)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var caseIds = cases.Select(c => c.Id).ToList();
            var defendants = await _context.Defendants
                .Where(d => caseIds.Contains(d.CaseId))
                .ToListAsync();

            return cases.Select(c => new CaseSummaryDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                OrderType = c.OrderType.ToString(),
                Status = c.Status.ToString(),
                DefendantName = defendants.FirstOrDefault(d => d.CaseId == c.Id)?.FullName ?? "Unknown",
                CreatedAt = c.CreatedAt
            }).ToList();
        }
        else if (user.Role == UserRole.Bank)
        {
            var cases = await _context.Cases
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var caseIds = cases.Select(c => c.Id).ToList();
            var defendants = await _context.Defendants
                .Where(d => caseIds.Contains(d.CaseId))
                .ToListAsync();

            return cases.Select(c => new CaseSummaryDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                OrderType = c.OrderType.ToString(),
                Status = c.Status.ToString(),
                DefendantName = defendants.FirstOrDefault(d => d.CaseId == c.Id)?.FullName ?? "Unknown",
                CreatedAt = c.CreatedAt
            }).ToList();
        }

        throw new UnauthorizedAccessException("User role not authorized.");
    }

    public async Task<object> GetCaseByIdAsync(int id)
    {
        var @case = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (@case == null) throw new KeyNotFoundException("Case not found");

        var complainant = await _context.Complainants.FirstOrDefaultAsync(c => c.CaseId == id);
        var defendant = await _context.Defendants.FirstOrDefaultAsync(d => d.CaseId == id);
        var documents = await _context.CaseDocuments.Where(d => d.CaseId == id).ToListAsync();

        return new
        {
            @case.Id,
            @case.CaseNumber,
            @case.OrderType,
            @case.FreezeAmount,
            @case.Status,
            @case.CreatedAt,
            Complainant = complainant,
            Defendant = defendant != null ? new
            {
                defendant.Id,
                defendant.CaseId,
                defendant.FullName,
                IdentityNumber = DataMaskingHelper.MaskIdentity(defendant.IdentityNumber),
                PanNumber = DataMaskingHelper.MaskIdentity(defendant.PanNumber),
                BankAccountNumber = DataMaskingHelper.MaskAccount(defendant.BankAccountNumber)
            } : null,
            Documents = documents
        };
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadDocumentAsync(string caseNumber, int documentId)
    {
        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);
        if (caseEntity == null) throw new KeyNotFoundException("Case not found");

        var doc = await _context.CaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == caseEntity.Id);

        if (doc == null)
            throw new KeyNotFoundException("Document not found");

        var stream = await _blobStorage.DownloadFileAsync(doc.FilePath);
        
        if (stream == null)
            throw new KeyNotFoundException("Document file not found in storage");

        string contentType = doc.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : doc.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || doc.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : doc.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "application/octet-stream";

        return (stream, contentType, doc.FileName);
    }
}
