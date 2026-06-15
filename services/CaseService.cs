using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ccms_backend.data;
using ccms_backend.dtos;

namespace ccms_backend.services;

public class CaseService
{
    private readonly ICaseRepository _caseRepository;

    public CaseService(ICaseRepository caseRepository)
    {
        _caseRepository = caseRepository;
    }

    public async Task<IEnumerable<CaseSummaryDto>> GetCasesForBankAsync()
    {
        var cases = await _caseRepository.GetCasesForBankAsync();
        return cases.Select(c => new CaseSummaryDto
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            OrderType = c.OrderType.ToString(),
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            DefendantName = c.Defendant?.FullName ?? "Unknown"
        });
    }
}