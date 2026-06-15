using System;

namespace ccms_backend.models;

public class CaseResponse
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public int? RespondedByUserId { get; set; }
    public ResponseType ResponseType { get; set; }
    public decimal? FreezeAmountApplied { get; set; }
    public decimal? BalanceReported { get; set; }
    public string? Remarks { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}