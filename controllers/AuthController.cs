using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;
using ccms_backend.services;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(AppDbContext context, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        // Verify password using BCrypt
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        // Generate JWT Token
        var token = _jwtTokenService.GenerateToken(user);

        // Determine redirection URL based on Role
        string redirectUrl = user.Role == UserRole.Court ? "/court/dashboard" : "/bank/dashboard";

        return Ok(new AuthResultDto
        {
            Token = token,
            Role = user.Role.ToString(),
            RedirectUrl = redirectUrl
        });
    }
}