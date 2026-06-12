/*
 * File: BatchJobLog.cs
 * Description: Represents an audit record written at the end of each batch run.
 * To Implement: Display on Bank dashboard.
 */

using System;

namespace ccms_backend.models;

public class BatchJobLog
{
    public int Id { get; set; }
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    public bool IsManualTrigger { get; set; }
    public int CasesProcessed { get; set; }
    public int CasesValidated { get; set; }
    public int CasesNotFound { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; } // Null if completed successfully
}
