/*
 * File: Case.cs
 * Description: Entity representing a court case.
 * To Implement: State validation rules and entity structure.
 */

using System;
using System.Collections.Generic;

namespace ccms_backend.models;

public class Case
{
    public int Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty; // Format: CCMS-YYYYMMDD-XXXX
    public OrderType OrderType { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Pending;
    public string ComplainantName { get; set; } = string.Empty;
    public string DefendantName { get; set; } = string.Empty;
    public string DefendantAadhaar { get; set; } = string.Empty;   // Stored raw; masked on retrieval
    public string DefendantPan { get; set; } = string.Empty;       // Stored raw; masked on retrieval
    public string DefendantAccountNumber { get; set; } = string.Empty; // Stored raw; masked on retrieval
    public string BankCode { get; set; } = string.Empty;
    public decimal? FreezeAmount { get; set; }                     // Set if OrderType = FreezeAccount
    public string? MatchedAccountNumber { get; set; }              // Set by batch validation
    public decimal? MatchedBalance { get; set; }                   // Set by batch validation
    public string? MatchedAccountStatus { get; set; }              // Set by batch validation
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CaseDocument> Documents { get; set; } = new List<CaseDocument>();
    public CaseResponse? Response { get; set; }
}
