using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CCMS.Application.DTOs;
using CCMS.Application.Services;

namespace CCMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly CaseService _caseService;
    private readonly ILogger<CasesController> _logger;

    public CasesController(CaseService caseService, ILogger<CasesController> logger)
    {
        _caseService = caseService;
        _logger = logger;
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

        try
        {
            using var courtOrderStream = courtOrderFile.OpenReadStream();
            using var aadhaarStream = aadhaarFile.OpenReadStream();
            using var panStream = panFile.OpenReadStream();

            var (caseNumber, id) = await _caseService.CreateCaseAsync(
                dto,
                courtOrderStream,
                courtOrderFile.FileName,
                aadhaarStream,
                aadhaarFile.FileName,
                aadhaarFile.ContentType,
                panStream,
                panFile.FileName,
                panFile.ContentType,
                username);

            return Ok(new { caseNumber, id });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the case.", error = ex.Message });
        }
    }

    [HttpGet("inbox")]
    [Authorize(Roles = "Bank")]
    public async Task<ActionResult<CaseInboxDto>> GetInbox()
    {
        var inbox = await _caseService.GetInboxAsync();
        return Ok(inbox);
    }

    [HttpGet("{caseNumber}")]
    [Authorize(Roles = "Court,Bank")]
    public async Task<ActionResult<CaseDetailDto>> GetCaseDetail(string caseNumber)
    {
        try
        {
            var dto = await _caseService.GetCaseDetailAsync(caseNumber);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{caseNumber}/response")]
    [Authorize(Roles = "Bank")]
    public async Task<IActionResult> SubmitResponse(string caseNumber, [FromBody] SubmitResponseDto payload)
    {
        int? userId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int parsedUserId))
        {
            userId = parsedUserId;
        }

        try
        {
            await _caseService.SubmitResponseAsync(caseNumber, payload, userId);
            return Ok(new { message = "Response submitted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists"))
                return Conflict(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCases()
    {
        var username = User.Identity?.Name ?? User.FindFirst("unique_name")?.Value;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _caseService.GetCasesAsync(username);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCaseById(int id)
    {
        try
        {
            var result = await _caseService.GetCaseByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{caseNumber}/documents/{documentId}/download")]
    [Authorize(Roles = "Court,Bank")]
    public async Task<IActionResult> DownloadDocument(string caseNumber, int documentId)
    {
        try
        {
            var (stream, contentType, fileName) = await _caseService.DownloadDocumentAsync(caseNumber, documentId);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
