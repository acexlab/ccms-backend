using System;
using System.Collections.Generic;

namespace CCMS.Application.DTOs;

public class CaseDetailDto
{
    public string CaseNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    public string ComplainantName { get; set; } = string.Empty;
    public string ComplainantIdentityNumber { get; set; } = string.Empty;

    public string DefendantName { get; set; } = string.Empty;
    public string DefendantAadhaar { get; set; } = string.Empty; // Masked
    public string DefendantPan { get; set; } = string.Empty; // Masked
    public string DefendantAccountNumber { get; set; } = string.Empty; // Masked

    public string? MatchedAccountNumber { get; set; } // Full
    public string? AccountStatus { get; set; }
    public decimal? CurrentBalance { get; set; }
    public DateTime? ValidationTimestamp { get; set; }

    public List<CaseDocumentDto> Documents { get; set; } = new();
    public CaseResponseDto? Response { get; set; }
}
