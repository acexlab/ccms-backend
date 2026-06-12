/*
 * File: User.cs
 * Description: Represents a system user (Court Officer or Bank Officer).
 * To Implement: Secure password hashes via BCrypt.
 */

namespace ccms_backend.models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? BankCode { get; set; } // Non-null for Bank Officers only
}
