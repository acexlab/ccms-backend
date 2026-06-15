using System.Collections.Generic;

namespace ccms_backend.dtos;

public class CaseInboxDto
{
    public List<CaseSummaryDto> AwaitingAction { get; set; } = new();
    public List<CaseSummaryDto> PendingBatch { get; set; } = new();
    public List<CaseSummaryDto> Completed { get; set; } = new();
    public List<CaseSummaryDto> AutoResolved { get; set; } = new();
}
