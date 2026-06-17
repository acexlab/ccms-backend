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
    private readonly ILogger<CasesController> _logger;
    private readonly IBlobStorageService _blobStorage;

    public CasesController(AppDbContext context, ILogger<CasesController> logger, IBlobStorageService blobStorage)
    {
        _context = context;
        _logger = logger;
        _blobStorage = blobStorage;
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

            // Generate realistic Court Order PDF based on case details
            var courtOrderBytes = ccms_backend.services.PdfGenerator.GenerateCourtOrder(dto, caseNumber);
            var uniqueCourtOrderName = $"{Guid.NewGuid()}_{courtOrderFile.FileName}";
            
            using (var ms = new MemoryStream(courtOrderBytes))
            {
                await _blobStorage.UploadFileAsync(ms, uniqueCourtOrderName, "application/pdf");
            }

            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.CourtOrder,
                FileName = courtOrderFile.FileName,
                FilePath = uniqueCourtOrderName,
                FileSize = courtOrderBytes.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save Aadhaar Copy File
            var uniqueAadhaarName = $"{Guid.NewGuid()}_{aadhaarFile.FileName}";
            using (var stream = aadhaarFile.OpenReadStream())
            {
                await _blobStorage.UploadFileAsync(stream, uniqueAadhaarName, aadhaarFile.ContentType);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.AadhaarCopy,
                FileName = aadhaarFile.FileName,
                FilePath = uniqueAadhaarName,
                FileSize = (int)aadhaarFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save PAN Copy File
            var uniquePanName = $"{Guid.NewGuid()}_{panFile.FileName}";
            using (var stream = panFile.OpenReadStream())
            {
                await _blobStorage.UploadFileAsync(stream, uniquePanName, panFile.ContentType);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.PANCopy,
                FileName = panFile.FileName,
                FilePath = uniquePanName,
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
    [Authorize(Roles = "Court,Bank")]
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

        var maskedIdentity = DataMaskingHelper.MaskIdentity(caseEntity.Defendant?.IdentityNumber) ?? "";
        var aadhaarPart = maskedIdentity;
        var panPart = "";
        if (maskedIdentity.Contains("PAN:")) {
            var parts = maskedIdentity.Split(new[] { ", PAN:" }, StringSplitOptions.None);
            aadhaarPart = parts[0].Replace("Aadhaar:", "").Trim();
            if (parts.Length > 1) panPart = parts[1].Trim();
        } else {
            aadhaarPart = aadhaarPart.Replace("Aadhaar:", "").Trim();
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

        if (caseEntity.Response != null)
            return Conflict(new { message = "A response already exists for this case." });

        if (caseEntity.Status != CaseStatus.AccountValidated)
            return BadRequest(new { message = "Case is not in AccountValidated status." });

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
            Defendant = defendant != null ? new
            {
                defendant.Id,
                defendant.CaseId,
                defendant.FullName,
                IdentityNumber = DataMaskingHelper.MaskIdentity(defendant.IdentityNumber),
                BankAccountNumber = DataMaskingHelper.MaskAccount(defendant.BankAccountNumber)
            } : null,
            Documents = documents
        });
    }

    [HttpGet("{caseNumber}/documents/{documentId}/download")]
    [Authorize(Roles = "Court,Bank")]
    public async Task<IActionResult> DownloadDocument(string caseNumber, int documentId)
    {
        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);
        if (caseEntity == null) return NotFound();

        var doc = await _context.CaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == caseEntity.Id);

        if (doc == null)
            return NotFound();

        var stream = await _blobStorage.DownloadFileAsync(doc.FilePath);
        
        if (stream == null)
        {
            if (doc.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // Initialize FailsafeFontResolver for Linux environments
                PdfSharp.Fonts.GlobalFontSettings.FontResolver ??= new PdfSharp.Snippets.Font.FailsafeFontResolver();

                var document = new PdfSharp.Pdf.PdfDocument();
                var page = document.AddPage();
                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                var font = new PdfSharp.Drawing.XFont("Arial", 16, PdfSharp.Drawing.XFontStyleEx.Bold);
                gfx.DrawString($"Simulated Document: {doc.FileName}", font, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(0, 0, page.Width, page.Height), PdfSharp.Drawing.XStringFormats.Center);
                
                using var ms = new System.IO.MemoryStream();
                document.Save(ms);
                return File(ms.ToArray(), "application/pdf", doc.FileName);
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes("Dummy file content for " + doc.FileName);
            return File(bytes, "application/octet-stream", doc.FileName);
        }

        var contentType = doc.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";

        return File(stream, contentType, doc.FileName);
    }
}