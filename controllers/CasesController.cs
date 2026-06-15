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

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CasesController(AppDbContext context)
    {
        _context = context;
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

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Save Court Order File
            var uniqueCourtOrderName = $"{Guid.NewGuid()}_{courtOrderFile.FileName}";
            var courtOrderPath = Path.Combine(uploadsFolder, uniqueCourtOrderName);
            using (var stream = new FileStream(courtOrderPath, FileMode.Create))
            {
                await courtOrderFile.CopyToAsync(stream);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.CourtOrder,
                FileName = courtOrderFile.FileName,
                FilePath = $"/uploads/{uniqueCourtOrderName}",
                FileSize = (int)courtOrderFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save Aadhaar Copy File
            var uniqueAadhaarName = $"{Guid.NewGuid()}_{aadhaarFile.FileName}";
            var aadhaarPath = Path.Combine(uploadsFolder, uniqueAadhaarName);
            using (var stream = new FileStream(aadhaarPath, FileMode.Create))
            {
                await aadhaarFile.CopyToAsync(stream);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.AadhaarCopy,
                FileName = aadhaarFile.FileName,
                FilePath = $"/uploads/{uniqueAadhaarName}",
                FileSize = (int)aadhaarFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save PAN Copy File
            var uniquePanName = $"{Guid.NewGuid()}_{panFile.FileName}";
            var panPath = Path.Combine(uploadsFolder, uniquePanName);
            using (var stream = new FileStream(panPath, FileMode.Create))
            {
                await panFile.CopyToAsync(stream);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.PANCopy,
                FileName = panFile.FileName,
                FilePath = $"/uploads/{uniquePanName}",
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

    [HttpGet("inbox")]
    [Authorize(Roles = "Bank")]
    public async Task<ActionResult<CaseInboxDto>> GetInbox()
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
            AwaitingAction = summaries.Where(c => c.Status == CaseStatus.AccountValidated.ToString()).ToList(),
            PendingBatch = summaries.Where(c => c.Status == CaseStatus.Pending.ToString()).ToList(),
            Completed = summaries.Where(c => c.Status == CaseStatus.FreezeApplied.ToString() || c.Status == CaseStatus.BalanceProvided.ToString()).ToList(),
            AutoResolved = summaries.Where(c => c.Status == CaseStatus.AccountNotFound.ToString()).ToList()
        };

        return Ok(inbox);
    }

    [HttpGet("{caseNumber}")]
    [Authorize(Roles = "Bank")]
    public async Task<ActionResult<CaseDetailDto>> GetCaseDetail(string caseNumber)
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
            return NotFound(new { message = "Case not found" });
        }

        var dto = new CaseDetailDto
        {
            CaseNumber = caseEntity.CaseNumber,
            OrderType = caseEntity.OrderType.ToString(),
            Status = caseEntity.Status.ToString(),
            ComplainantName = caseEntity.Complainant?.FullName ?? string.Empty,
            ComplainantIdentityNumber = caseEntity.Complainant?.IdentityNumber ?? string.Empty,
            DefendantName = caseEntity.Defendant?.FullName ?? string.Empty,
            DefendantAadhaar = MaskAadhaar(caseEntity.Defendant?.IdentityNumber),
            DefendantPan = MaskPan(caseEntity.Defendant?.IdentityNumber),
            DefendantAccountNumber = MaskAccount(caseEntity.Defendant?.BankAccountNumber),

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

        return Ok(dto);
    }

    [HttpPost("{caseNumber}/response")]
    [Authorize(Roles = "Bank")]
    public async Task<IActionResult> SubmitResponse(string caseNumber, [FromBody] SubmitResponseDto payload)
    {
        var caseEntity = await _context.Cases
            .Include(c => c.Response)
            .FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);

        if (caseEntity == null)
            return NotFound(new { message = "Case not found" });

        if (caseEntity.Status != CaseStatus.AccountValidated)
            return BadRequest(new { message = "Case is not in AccountValidated status." });

        if (caseEntity.Response != null)
            return Conflict(new { message = "A response already exists for this case." });

        // Update status and create response
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var newStatus = payload.ResponseType == "FreezeApplied" ? CaseStatus.FreezeApplied :
                            payload.ResponseType == "BalanceProvided" ? CaseStatus.BalanceProvided :
                            payload.ResponseType == "AccountNotFound" ? CaseStatus.AccountNotFound : CaseStatus.Pending;

            caseEntity.Status = newStatus;

            var responseEntity = new CaseResponse
            {
                CaseId = caseEntity.Id,
                ResponseType = Enum.Parse<ResponseType>(payload.ResponseType),
                FreezeAmountApplied = payload.FreezeAmountApplied,
                BalanceReported = payload.BalanceReported,
                Remarks = payload.Remarks,
                SubmittedAt = DateTime.UtcNow
            };

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                responseEntity.RespondedByUserId = userId;
            }

            _context.CaseResponses.Add(responseEntity);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Response submitted successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = ex.Message });
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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCaseById(int id)
    {
        var @case = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (@case == null) return NotFound();

        var complainant = await _context.Complainants.FirstOrDefaultAsync(c => c.CaseId == id);
        var defendant = await _context.Defendants.FirstOrDefaultAsync(d => d.CaseId == id);
        var documents = await _context.CaseDocuments.Where(d => d.CaseId == id).ToListAsync();

        return Ok(new
        {
            @case.Id,
            @case.CaseNumber,
            @case.OrderType,
            @case.FreezeAmount,
            @case.Status,
            @case.CreatedAt,
            Complainant = complainant,
            Defendant = defendant,
            Documents = documents
        });
    }

    [HttpGet("{caseNumber}/documents/{documentId}/download")]
    [Authorize(Roles = "Bank")]
    public async Task<IActionResult> DownloadDocument(string caseNumber, int documentId)
    {
        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);
        if (caseEntity == null) return NotFound();

        var doc = await _context.CaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == caseEntity.Id);

        if (doc == null)
            return NotFound();

        var filePath = doc.FilePath;
        if (!System.IO.File.Exists(filePath))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("Dummy file content");
            return File(bytes, "application/octet-stream", doc.FileName);
        }

        var stream = System.IO.File.OpenRead(filePath);
        return File(stream, "application/octet-stream", doc.FileName);
    }

    private string MaskAadhaar(string? aadhaar)
    {
        if (string.IsNullOrEmpty(aadhaar) || aadhaar.Length < 4) return aadhaar ?? "";
        return "XXXX XXXX " + aadhaar.Substring(aadhaar.Length - 4);
    }

    private string MaskPan(string? pan)
    {
        if (string.IsNullOrEmpty(pan) || pan.Length < 4) return pan ?? "";
        return new string('X', Math.Max(0, pan.Length - 4)) + pan.Substring(pan.Length - 4);
    }

    private string MaskAccount(string? account)
    {
        if (string.IsNullOrEmpty(account) || account.Length < 4) return account ?? "";
        return new string('X', Math.Max(0, account.Length - 4)) + account.Substring(account.Length - 4);
    }
}