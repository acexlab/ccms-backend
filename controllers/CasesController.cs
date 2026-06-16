/*
 * File: CasesController.cs
 * Description: Controller managing Case creation, listing, retrieval, and bank responses.
 * To Implement: Keep role mapping safe and secure.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;
using ccms_backend.services;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public CasesController(AppDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    [HttpPost]
    [Authorize(Roles = "Court")]
    public async Task<IActionResult> CreateCase(
        [FromForm] CreateCaseDto dto,
        IFormFile courtOrderFile,
        IFormFile aadhaarFile,
        IFormFile panFile)
    {
        if (dto == null)
        {
            return BadRequest(new { message = "Invalid case data." });
        }

        // Validate mandatory files
        if (courtOrderFile == null || courtOrderFile.Length == 0)
        {
            return BadRequest(new { message = "Court Order PDF file is required." });
        }
        if (aadhaarFile == null || aadhaarFile.Length == 0)
        {
            return BadRequest(new { message = "Aadhaar Copy file is required." });
        }
        if (panFile == null || panFile.Length == 0)
        {
            return BadRequest(new { message = "PAN Copy file is required." });
        }

        // String validations (Regex patterns)
        var nameRegex = new Regex(@"^[a-zA-Z\s]{3,100}$");
        var idRegex = new Regex(@"^(?:\d{12}|[a-zA-Z]{5}[0-9]{4}[a-zA-Z]{1})$");
        var accountRegex = new Regex(@"^\d{9,18}$");

        if (!nameRegex.IsMatch(dto.ComplainantName ?? ""))
        {
            return BadRequest(new { message = "Complainant Name must contain only letters and spaces, between 3 and 100 characters." });
        }
        if (!idRegex.IsMatch(dto.ComplainantId ?? ""))
        {
            return BadRequest(new { message = "Complainant Identity Number must be a valid 12-digit Aadhaar or 10-char PAN." });
        }
        if (!nameRegex.IsMatch(dto.DefendantName ?? ""))
        {
            return BadRequest(new { message = "Defendant Name must contain only letters and spaces, between 3 and 100 characters." });
        }
        if (!idRegex.IsMatch(dto.DefendantId ?? ""))
        {
            return BadRequest(new { message = "Defendant Identity Number must be a valid 12-digit Aadhaar or 10-char PAN." });
        }
        if (!accountRegex.IsMatch(dto.DefendantAccountNumber ?? ""))
        {
            return BadRequest(new { message = "Defendant Bank Account Number must contain between 9 and 18 digits." });
        }
        if (!nameRegex.IsMatch(dto.DefendantBankName ?? ""))
        {
            return BadRequest(new { message = "Defendant Bank Name must contain only letters and spaces, between 3 and 100 characters." });
        }

        var username = User.Identity?.Name ?? User.FindFirst("unique_name")?.Value;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return Unauthorized();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
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

            var defendant = new Defendant
            {
                CaseId = @case.Id,
                FullName = dto.DefendantName,
                IdentityNumber = dto.DefendantId,
                BankAccountNumber = dto.DefendantAccountNumber,
                BankName = dto.DefendantBankName
            };
            _context.Defendants.Add(defendant);

            // Save files to Azure Blob Storage using FileStorageService
            var year = DateTime.UtcNow.Year.ToString();
            
            // Court Order File upload
            var courtOrderExt = Path.GetExtension(courtOrderFile.FileName);
            var courtOrderBlobName = $"{year}/case-{@case.Id}/{Guid.NewGuid()}_court_order{courtOrderExt}";
            using (var stream = courtOrderFile.OpenReadStream())
            {
                await _fileStorageService.UploadFileAsync(stream, courtOrderBlobName, courtOrderFile.ContentType);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.CourtOrder,
                FileName = courtOrderFile.FileName,
                FilePath = courtOrderBlobName,
                BlobName = courtOrderBlobName,
                ContentType = courtOrderFile.ContentType,
                FileSize = (int)courtOrderFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Aadhaar Copy File upload
            var aadhaarExt = Path.GetExtension(aadhaarFile.FileName);
            var aadhaarBlobName = $"{year}/case-{@case.Id}/{Guid.NewGuid()}_aadhaar{aadhaarExt}";
            using (var stream = aadhaarFile.OpenReadStream())
            {
                await _fileStorageService.UploadFileAsync(stream, aadhaarBlobName, aadhaarFile.ContentType);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.AadhaarCopy,
                FileName = aadhaarFile.FileName,
                FilePath = aadhaarBlobName,
                BlobName = aadhaarBlobName,
                ContentType = aadhaarFile.ContentType,
                FileSize = (int)aadhaarFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // PAN Copy File upload
            var panExt = Path.GetExtension(panFile.FileName);
            var panBlobName = $"{year}/case-{@case.Id}/{Guid.NewGuid()}_pan{panExt}";
            using (var stream = panFile.OpenReadStream())
            {
                await _fileStorageService.UploadFileAsync(stream, panBlobName, panFile.ContentType);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.PANCopy,
                FileName = panFile.FileName,
                FilePath = panBlobName,
                BlobName = panBlobName,
                ContentType = panFile.ContentType,
                FileSize = (int)panFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { caseNumber = @case.CaseNumber, id = @case.Id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "An error occurred while creating the case.", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCases()
    {
        var username = User.Identity?.Name ?? User.FindFirst("unique_name")?.Value;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return Unauthorized();

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

            var result = cases.Select(c => new CaseSummaryDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                OrderType = c.OrderType.ToString(),
                Status = c.Status.ToString(),
                DefendantName = defendants.FirstOrDefault(d => d.CaseId == c.Id)?.FullName ?? "Unknown",
                CreatedAt = c.CreatedAt
            }).ToList();

            return Ok(result);
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

            var result = cases.Select(c => new CaseSummaryDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                OrderType = c.OrderType.ToString(),
                Status = c.Status.ToString(),
                DefendantName = defendants.FirstOrDefault(d => d.CaseId == c.Id)?.FullName ?? "Unknown",
                CreatedAt = c.CreatedAt
            }).ToList();

            return Ok(result);
        }

        return Forbid();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCaseById(int id)
    {
        var @case = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (@case == null) return NotFound();

        var complainant = await _context.Complainants.FirstOrDefaultAsync(c => c.CaseId == id);
        var defendant = await _context.Defendants.FirstOrDefaultAsync(d => d.CaseId == id);
        var documents = await _context.CaseDocuments.Where(d => d.CaseId == id).ToListAsync();
        foreach (var doc in documents)
        {
            if (!string.IsNullOrEmpty(doc.BlobName))
            {
                try
                {
                    doc.FilePath = _fileStorageService.GenerateSasUri(doc.BlobName);
                }
                catch (Exception ex)
                {
                    doc.FilePath = $"#error-sas-{ex.Message}";
                }
            }
        }
        var response = await _context.CaseResponses.FirstOrDefaultAsync(r => r.CaseId == id);
        var validationResult = await _context.CaseValidationResults.FirstOrDefaultAsync(v => v.CaseId == id);

        object? maskedDefendant = null;
        if (defendant != null)
        {
            maskedDefendant = new
            {
                defendant.Id,
                defendant.CaseId,
                defendant.FullName,
                IdentityNumber = ccms_backend.services.DataMaskingHelper.MaskIdentity(defendant.IdentityNumber),
                BankAccountNumber = ccms_backend.services.DataMaskingHelper.MaskAccount(defendant.BankAccountNumber),
                defendant.BankName
            };
        }

        return Ok(new
        {
            @case.Id,
            @case.CaseNumber,
            @case.OrderType,
            @case.FreezeAmount,
            @case.Status,
            @case.CreatedAt,
            Complainant = complainant,
            Defendant = maskedDefendant,
            Documents = documents,
            Response = response,
            ValidationResult = validationResult
        });
    }

    [HttpPost("{id}/response")]
    [Authorize(Roles = "Bank")]
    public async Task<IActionResult> SubmitResponse(int id, [FromBody] SubmitResponseDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { message = "Invalid response data." });
        }

        var @case = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (@case == null)
        {
            throw new CaseNotFoundException($"Case with ID {id} was not found.");
        }

        // Duplicate response check: check if state is terminal
        if (@case.Status == CaseStatus.AccountNotFound || 
            @case.Status == CaseStatus.FreezeApplied || 
            @case.Status == CaseStatus.BalanceProvided)
        {
            throw new CaseAlreadyRespondedException("This case has already received a terminal response and cannot be modified.");
        }

        // Check if case is in AccountValidated state
        if (@case.Status != CaseStatus.AccountValidated)
        {
            throw new UnauthorisedActionException("Responses can only be submitted for cases in 'AccountValidated' status.");
        }

        // Parse ResponseType from DTO
        if (!Enum.TryParse<ResponseType>(dto.ResponseType, true, out var parsedResponseType))
        {
            return BadRequest(new { message = $"Invalid response type '{dto.ResponseType}'." });
        }

        // Validate values according to response type
        if (parsedResponseType == ResponseType.FreezeApplied)
        {
            if (!dto.FreezeAmountApplied.HasValue || dto.FreezeAmountApplied < 0)
            {
                return BadRequest(new { message = "A valid non-negative Freeze Amount is required for a Freeze response." });
            }
        }
        else if (parsedResponseType == ResponseType.BalanceProvided)
        {
            if (!dto.BalanceReported.HasValue || dto.BalanceReported < 0)
            {
                return BadRequest(new { message = "A valid non-negative Account Balance is required for a Balance Enquiry response." });
            }
        }

        var username = User.Identity?.Name ?? User.FindFirst("unique_name")?.Value;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        int? bankOfficerId = user?.Id;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Map case response
            var response = new CaseResponse
            {
                CaseId = @case.Id,
                RespondedByUserId = bankOfficerId,
                ResponseType = parsedResponseType,
                FreezeAmountApplied = parsedResponseType == ResponseType.FreezeApplied ? dto.FreezeAmountApplied : null,
                BalanceReported = parsedResponseType == ResponseType.BalanceProvided ? dto.BalanceReported : null,
                Remarks = dto.Remarks,
                SubmittedAt = DateTime.UtcNow
            };

            _context.CaseResponses.Add(response);

            // Update case status
            @case.Status = parsedResponseType == ResponseType.FreezeApplied 
                ? CaseStatus.FreezeApplied 
                : CaseStatus.BalanceProvided;
            @case.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true, message = "Response submitted successfully." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "An error occurred while submitting the response.", error = ex.Message });
        }
    }

    [HttpDelete("documents/{id}")]
    [Authorize(Roles = "Court")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var document = await _context.CaseDocuments.FindAsync(id);
        if (document == null) return NotFound();

        try
        {
            if (!string.IsNullOrEmpty(document.BlobName))
            {
                await _fileStorageService.DeleteFileAsync(document.BlobName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete blob {document.BlobName}: {ex.Message}");
        }

        _context.CaseDocuments.Remove(document);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}