/*
 * File: AuthService.cs
 * Description: Service handling user login verification and JWT token issuance.
 * To Implement: Keep login checks secure.
 */

using System.Threading;
using System.Threading.Tasks;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;

namespace ccms_backend.services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(IUserRepository userRepository, IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUsernameAsync(dto.Username, ct);
        if (user == null)
        {
            throw new UnauthorisedActionException("Invalid username or password.");
        }

        // Verify password hash
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw new UnauthorisedActionException("Invalid username or password.");
        }

        var token = _jwtTokenService.GenerateToken(user);

        return new AuthResultDto
        {
            Token = token,
            Role = user.Role.ToString()
        };
    }
}
