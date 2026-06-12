/*
 * File: CaseDetailDto.cs
 * Description: Detailed view of a Court Case. Sensitive fields are masked before sending.
 * To Implement: Mapping in CaseService.
 */

using System;
using System.Collections.Generic;

namespace ccms_backend.dtos;

public class CaseDetailDto
{
    public int Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ComplainantName { get; set; } = string.Empty;
    public string DefendantName { get; set; } = string.Empty;
    public string DefendantAadhaar { get; set; } = string.Empty;   // Masked
    public string DefendantPan { get; set; } = string.Empty;       // Masked
    public string DefendantAccountNumber { get; set; } = string.Empty; // Masked
    public string BankCode { get; set; } = string.Empty;
    public decimal? FreezeAmount { get; set; }
    public string? MatchedAccountNumber { get; set; }
    public decimal? MatchedBalance { get; set; }
    public string? MatchedAccountStatus { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<CaseDocumentDto> Documents { get; set; } = new();
    public CaseResponseDto? Response { get; set; }
}
