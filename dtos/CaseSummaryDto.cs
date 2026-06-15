using System;

namespace ccms_backend.dtos;

public class CaseSummaryDto
{
    public string CaseNumber { get; set; } = string.Empty;
    public string DefendantName { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ValidationDate { get; set; }
}