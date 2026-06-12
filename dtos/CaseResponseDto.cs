/*
 * File: CaseResponseDto.cs
 * Description: Data transfer object representing the bank's formal case response.
 * To Implement: Contains validation responses for freeze or balance enquiries.
 */

using System;

namespace ccms_backend.dtos;

public class CaseResponseDto
{
    public string ResponseType { get; set; } = string.Empty; // "FreezeApplied" | "BalanceProvided" | "AccountNotFound"
    public decimal? ReportedAmount { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public bool IsSystemGenerated { get; set; }
    public DateTime RespondedAt { get; set; }
}
