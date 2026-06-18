using CCMS.Domain.Enums;
using System;

namespace CCMS.Domain.Entities;

public class BatchJobLog
{
    public int Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public TriggeredBy TriggeredBy { get; set; }
    public int? TriggeredByUserId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public int CasesProcessed { get; set; }
    public int AccountsMatched { get; set; }
    public int AccountsNotFound { get; set; }
    public int? DurationSeconds { get; set; }
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Running;
}
