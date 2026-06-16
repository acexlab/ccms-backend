using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ccms_backend.data;
using ccms_backend.models;
using ccms_backend.services;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public AttachmentsController(AppDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    [HttpGet("{id:int}/download")]
    [Authorize(Roles = "Court,Bank")]
    public async Task<IActionResult> Download(int id, [FromQuery] bool inline = false)
    {
        var doc = await _context.CaseDocuments
            .Include(d => d.Case)
                .ThenInclude(c => c.Defendant)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null || doc.Case == null)
        {
            return NotFound(new { message = "Attachment not found." });
        }

        var username = User.Identity?.Name ?? User.FindFirst("unique_name")?.Value;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return Unauthorized();

        // Enforce role-based boundaries
        if (user.Role == UserRole.Court)
        {
            if (doc.Case.CreatedByUserId != user.Id)
            {
                return Forbid();
            }
        }
        else if (user.Role == UserRole.Bank)
        {
            // Allow access to all documents for bank role
        }
        else
        {
            return Forbid();
        }

        try
        {
            var stream = await _fileStorage.GetFileAsync(doc.FilePath);
            var contentType = GetContentType(doc.FileName);

            var disposition = inline ? "inline" : "attachment";
            Response.Headers.Append("Content-Disposition", $"{disposition}; filename=\"{doc.FileName}\"");

            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "Physical file not found on storage." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while streaming the file.", error = ex.Message });
        }
    }

    private string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
