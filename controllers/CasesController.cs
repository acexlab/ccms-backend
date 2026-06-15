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
    public async Task<IActionResult> CreateCase([FromForm] CreateCaseDto dto, [FromForm] List<IFormFile> files)
    {
        if (dto == null)
        {
            return BadRequest("Invalid case data.");
        }

        var uploadedFiles = files ?? Request.Form.Files.ToList();
        if (uploadedFiles == null || uploadedFiles.Count == 0)
        {
            return BadRequest("Court Order File is required.");
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

            for (int i = 0; i < uploadedFiles.Count; i++)
            {
                var file = uploadedFiles[i];
                if (file.Length == 0) continue;

                var docType = DocumentType.CourtOrder;
                if (i == 1) docType = DocumentType.AadhaarCopy;
                else if (i == 2) docType = DocumentType.PANCopy;

                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var document = new CaseDocument
                {
                    CaseId = @case.Id,
                    DocumentType = docType,
                    FileName = file.FileName,
                    FilePath = $"/uploads/{uniqueFileName}",
                    FileSize = (int)file.Length,
                    UploadedAt = DateTime.UtcNow
                };
                _context.CaseDocuments.Add(document);
            }

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
            return Ok(cases);
        }
        else if (user.Role == UserRole.Bank)
        {
            var cases = await _context.Cases
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return Ok(cases);
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