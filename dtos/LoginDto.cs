/*
 * File: LoginDto.cs
 * Description: Request body containing credentials for user login.
 * To Implement: Keep in sync with user entity.
 */

namespace ccms_backend.dtos;

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
