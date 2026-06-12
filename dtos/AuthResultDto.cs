/*
 * File: AuthResultDto.cs
 * Description: Response payload returned on a successful authentication request.
 * To Implement: Keep in sync with jwt generation and roles.
 */

namespace ccms_backend.dtos;

public class AuthResultDto
{
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
