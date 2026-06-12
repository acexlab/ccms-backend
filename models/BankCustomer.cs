/*
 * File: BankCustomer.cs
 * Description: Represents a seeded bank customer profile, used by batch jobs for verification.
 * To Implement: Seed sufficient data in the dev environment for account matching.
 */

namespace ccms_backend.models;

public class BankCustomer
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AadhaarNumber { get; set; } = string.Empty;
    public string PanNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string AccountStatus { get; set; } = "Active"; // "Active" | "Frozen" | "Closed"
    public string BankCode { get; set; } = string.Empty;
}
