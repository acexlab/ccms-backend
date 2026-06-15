namespace ccms_backend.dtos;

public class AuthResultDto
{
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}