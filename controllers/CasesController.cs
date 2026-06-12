/*
 * File: CasesController.cs
 * Description: Controller managing Case creation, listing, retrieval, and bank responses.
 * To Implement: Keep role mapping safe and secure.
 */

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ccms_backend.dtos;
using ccms_backend.models;
using ccms_backend.services;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly CaseService _caseService;

    public CasesController(CaseService caseService)
    {
        _caseService = caseService;
    }

    [HttpPost]
    [Authorize(Roles = "CourtOfficer")]
    public async Task<IActionResult> CreateCase(
        [FromForm] CreateCaseDto dto,
        IFormFile courtOrderFile,
        IFormFile aadhaarFile,
        IFormFile panFile)
    {
        if (courtOrderFile == null || courtOrderFile.Length == 0)
            return BadRequest("Court Order File is required.");
        if (aadhaarFile == null || aadhaarFile.Length == 0)
            return BadRequest("Aadhaar File is required.");
        if (panFile == null || panFile.Length == 0)
            return BadRequest("PAN File is required.");

        var userId = GetUserId();

        using var courtOrderStream = courtOrderFile.OpenReadStream();
        using var aadhaarStream = aadhaarFile.OpenReadStream();
        using var panStream = panFile.OpenReadStream();

        var result = await _caseService.CreateCaseAsync(
            dto,
            courtOrderStream, courtOrderFile.FileName,
            aadhaarStream, aadhaarFile.FileName,
            panStream, panFile.FileName,
            userId,
            HttpContext.RequestAborted);

        return CreatedAtAction(nameof(GetCaseById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCases()
    {
        var role = GetUserRole();
        if (role == UserRole.CourtOfficer.ToString())
        {
            var userId = GetUserId();
            var result = await _caseService.GetMyCasesAsync(userId, HttpContext.RequestAborted);
            return Ok(result);
        }
        else if (role == UserRole.BankOfficer.ToString())
        {
            var bankCode = GetUserBankCode();
            if (string.IsNullOrEmpty(bankCode))
            {
                return Forbid("Bank Officer is not associated with any bank.");
            }
            var result = await _caseService.GetCasesForBankAsync(bankCode, HttpContext.RequestAborted);
            return Ok(result);
        }

        return Forbid();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCaseById(int id)
    {
        var userId = GetUserId();
        var role = GetUserRole();
        var bankCode = GetUserBankCode();

        var result = await _caseService.GetCaseByIdAsync(id, userId, role, bankCode, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("{id}/response")]
    [Authorize(Roles = "BankOfficer")]
    public async Task<IActionResult> SubmitResponse(int id, [FromBody] SubmitResponseDto dto)
    {
        var userId = GetUserId();
        await _caseService.SubmitResponseAsync(id, dto, userId, HttpContext.RequestAborted);
        return NoContent();
    }

    // Helper methods to parse current user context from claims
    private int GetUserId()
    {
        var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        return int.TryParse(val, out int id) ? id : 0;
    }

    private string GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value 
            ?? User.FindFirst("role")?.Value 
            ?? string.Empty;
    }

    private string? GetUserBankCode()
    {
        return User.FindFirst("bankCode")?.Value;
    }
}
