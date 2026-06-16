using System;

namespace ccms_backend.dtos;

public class CaseResponseDto
{
    public string ResponseType { get; set; } = string.Empty;
    public decimal? FreezeAmountApplied { get; set; }
    public decimal? BalanceReported { get; set; }
    public string? Remarks { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? ProcessedBy { get; set; }
}