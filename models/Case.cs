using System;
using System.Collections.Generic;

namespace ccms_backend.models;

public class Case
{
    public int Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public OrderType OrderType { get; set; }
    public decimal? FreezeAmount { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Defendant? Defendant { get; set; }
    public Complainant? Complainant { get; set; }
    public ICollection<CaseDocument> Documents { get; set; } = new List<CaseDocument>();
    public CaseResponse? Response { get; set; }
    public CaseValidationResult? ValidationResult { get; set; }
}