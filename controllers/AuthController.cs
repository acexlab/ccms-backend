using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
        var token = GenerateJwtToken(user);

        // Determine redirection URL based on Role
        string redirectUrl = user.Role == UserRole.Court ? "/court/dashboard" : "/bank/dashboard";

        return Ok(new AuthResultDto
        {
            Token = token,
            Role = user.Role.ToString(),
            RedirectUrl = redirectUrl
        });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"] ?? "SuperSecretJWTKey12345678901234567890";
        var issuer = jwtSettings["Issuer"] ?? "CCMS.API";
        var audience = jwtSettings["Audience"] ?? "CCMS.Client";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("unique_name", user.Username),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}