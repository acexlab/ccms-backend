using CCMS.Domain.Enums;
using System;

namespace CCMS.Domain.Entities;

public class CaseValidationResult
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string MatchedAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public MatchedOn MatchedOn { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}
