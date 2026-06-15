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
}