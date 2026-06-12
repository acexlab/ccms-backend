/*
 * File: BatchJobLogDto.cs
 * Description: Data transfer object representing the execution audit log of a background batch job.
 * To Implement: Keep in sync with BatchJobLog entity.
 */

using System;

namespace ccms_backend.dtos;

public class BatchJobLogDto
{
    public DateTime RunAt { get; set; }
    public bool IsManualTrigger { get; set; }
    public int CasesProcessed { get; set; }
    public int CasesValidated { get; set; }
    public int CasesNotFound { get; set; }
    public long DurationMs { get; set; }
}
