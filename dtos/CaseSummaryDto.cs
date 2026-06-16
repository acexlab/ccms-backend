using System;

namespace ccms_backend.dtos;

public class CaseSummaryDto
{
    public int Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DefendantName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidationDate { get; set; }
}