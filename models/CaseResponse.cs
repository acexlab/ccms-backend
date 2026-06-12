/*
 * File: CaseResponse.cs
 * Description: Represents a Bank Officer's formal response, or a system-generated "AccountNotFound" response.
 * To Implement: Keep in sync with Case terminal states.
 */

using System;

namespace ccms_backend.models;

public class CaseResponse
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string ResponseType { get; set; } = string.Empty; // "FreezeApplied" | "BalanceProvided" | "AccountNotFound"
    public decimal? ReportedAmount { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public bool IsSystemGenerated { get; set; } = false;     // true = created automatically by batch job
    public int? RespondedByUserId { get; set; }
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;

    public Case Case { get; set; } = null!;
}
