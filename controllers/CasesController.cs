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
using AutoMapper;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IUserRepository _userRepository;
    private readonly IBankCustomerRepository _bankCustomerRepository;
    private readonly IMapper _mapper;

    public CasesController(AppDbContext context, IFileStorageService fileStorage, IUserRepository userRepository, IBankCustomerRepository bankCustomerRepository, IMapper mapper)
    {
        _context = context;
        _fileStorage = fileStorage;
        _userRepository = userRepository;
        _bankCustomerRepository = bankCustomerRepository;
        _mapper = mapper;
    }

    [HttpPost]
    [Authorize(Roles = "Court")]
    public async Task<IActionResult> CreateCase(
        [FromForm] CreateCaseDto dto,
        IFormFile aadhaarFile,
        IFormFile panFile,
        IFormFile courtOrderFile)
    {
        if (dto == null)
        {
            return BadRequest(new { message = "Invalid case data." });
        }

        // Validate mandatory files
        if (aadhaarFile == null || aadhaarFile.Length == 0)
        {
            return BadRequest(new { message = "Aadhaar Copy file is required." });
        }
        if (panFile == null || panFile.Length == 0)
        {
            return BadRequest(new { message = "PAN Copy file is required." });
        }
        if (courtOrderFile == null || courtOrderFile.Length == 0)
        {
            return BadRequest(new { message = "Court Order file is required." });
        }

        // Validate file extensions, size limits and filename security
        var allowedExtensions = new[] { ".pdf", ".jpg", ".png" };
        var uploadedFiles = new[] { aadhaarFile, panFile, courtOrderFile };
        foreach (var file in uploadedFiles)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(new { message = $"Unsupported file format. Only PDF, JPG and PNG are allowed." });
            }
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "File size exceeds the maximum limit of 5 MB." });
            }
            if (file.FileName.Contains("..") || file.FileName.Contains("/") || file.FileName.Contains("\\"))
            {
                return BadRequest(new { message = "Malicious file name detected." });
            }
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

        var user = await _userRepository.GetByUsernameAsync(username);
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

            // Save Court Order File using file storage
            string courtOrderStoredPath;
            using (var courtOrderStream = courtOrderFile.OpenReadStream())
            {
                courtOrderStoredPath = await _fileStorage.SaveFileAsync(courtOrderStream, courtOrderFile.FileName);
            }

            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.CourtOrder,
                FileName = courtOrderFile.FileName,
                FilePath = courtOrderStoredPath,
                FileSize = (int)courtOrderFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save Aadhaar Copy File using file storage
            string aadhaarStoredPath;
            using (var aadhaarStream = aadhaarFile.OpenReadStream())
            {
                aadhaarStoredPath = await _fileStorage.SaveFileAsync(aadhaarStream, aadhaarFile.FileName);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.AadhaarCopy,
                FileName = aadhaarFile.FileName,
                FilePath = aadhaarStoredPath,
                FileSize = (int)aadhaarFile.Length,
                UploadedAt = DateTime.UtcNow
            });

            // Save PAN Copy File using file storage
            string panStoredPath;
            using (var panStream = panFile.OpenReadStream())
            {
                panStoredPath = await _fileStorage.SaveFileAsync(panStream, panFile.FileName);
            }
            _context.CaseDocuments.Add(new CaseDocument
            {
                CaseId = @case.Id,
                DocumentType = DocumentType.PANCopy,
                FileName = panFile.FileName,
                FilePath = panStoredPath,
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
        var query = _context.Cases
            .Include(c => c.Defendant)
            .Include(c => c.ValidationResult)
            .AsQueryable();

        var cases = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

        var summaries = _mapper.Map<List<CaseSummaryDto>>(cases);

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
        var username = User.Identity?.Name ?? User.FindFirst("unique_name")?.Value;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null) return Unauthorized();

        var caseEntity = await _context.Cases
            .Include(c => c.Complainant)
            .Include(c => c.Defendant)
            .Include(c => c.ValidationResult)
            .Include(c => c.Documents)
            .Include(c => c.Response)
                .ThenInclude(r => r.RespondedByUser)
            .FirstOrDefaultAsync(c => c.CaseNumber == caseNumber);

        if (caseEntity == null)
        {
            return NotFound(new { message = "Case not found" });
        }

        // Access checks
        if (user.Role == UserRole.Court)
        {
            if (caseEntity.CreatedByUserId != user.Id)
            {
                return Forbid();
            }
        }
        else if (user.Role == UserRole.Bank)
        {
            // Allow access to all cases for bank role
        }
        else
        {
            return Forbid();
        }

        var dto = _mapper.Map<CaseDetailDto>(caseEntity);

        return Ok(dto);
    }

    [HttpPost("{caseNumber}/response")]
    [Authorize(Roles = "Bank")]
    public async Task<IActionResult> SubmitResponse(string caseNumber, [FromBody] SubmitResponseDto payload)
    {
        var caseEntity = await _context.Cases
            .Include(c => c.Response)
            .Include(c => c.ValidationResult)
            .Include(c => c.Defendant)
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
            if (payload.ResponseType == "FreezeApplied")
            {
                if (payload.FreezeAmountApplied == null || payload.FreezeAmountApplied <= 0)
                {
                    return BadRequest(new { message = "Freeze amount must be greater than zero." });
                }

                if (caseEntity.ValidationResult == null)
                {
                    return BadRequest(new { message = "Case validation result is missing." });
                }

                var customer = await _bankCustomerRepository.GetByAccountNumberAsync(caseEntity.ValidationResult.MatchedAccountNumber);

                if (customer == null)
                {
                    return BadRequest(new { message = "Associated bank customer not found." });
                }

                if (payload.FreezeAmountApplied > customer.AvailableBalance)
                {
                    return BadRequest(new { message = "Freeze amount cannot exceed available balance." });
                }

                customer.AvailableBalance -= payload.FreezeAmountApplied.Value;
                customer.FrozenAmount += payload.FreezeAmountApplied.Value;
            }

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

        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null) return Unauthorized();

        if (user.Role == UserRole.Court)
        {
            var cases = await _context.Cases
                .Include(c => c.Defendant)
                .Include(c => c.ValidationResult)
                .Where(c => c.CreatedByUserId == user.Id)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var result = _mapper.Map<List<CaseSummaryDto>>(cases);
            return Ok(result);
        }
        else if (user.Role == UserRole.Bank)
        {
            var cases = await _context.Cases
                .Include(c => c.Defendant)
                .Include(c => c.ValidationResult)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var result = _mapper.Map<List<CaseSummaryDto>>(cases);
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

        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(physicalPath))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("Dummy file content");
            return File(bytes, "application/octet-stream", doc.FileName);
        }

        var contentType = doc.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";

        var stream = System.IO.File.OpenRead(physicalPath);
        return File(stream, contentType, doc.FileName);
    }

    public static string MaskAadhaar(string? aadhaar) => DataMaskingHelper.MaskAadhaar(aadhaar);
    public static string MaskPan(string? pan) => DataMaskingHelper.MaskPan(pan);
    public static string MaskAccount(string? account) => DataMaskingHelper.MaskAccount(account);
}