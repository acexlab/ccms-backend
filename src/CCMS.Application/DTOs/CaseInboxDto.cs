using System.Collections.Generic;

namespace CCMS.Application.DTOs;

public class CaseInboxDto
{
    public List<CaseSummaryDto> AwaitingAction { get; set; } = new();
    public List<CaseSummaryDto> PendingBatch { get; set; } = new();
    public List<CaseSummaryDto> Completed { get; set; } = new();
    public List<CaseSummaryDto> AutoResolved { get; set; } = new();
}
