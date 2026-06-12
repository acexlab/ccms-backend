/*
 * File: CaseNumberGenerator.cs
 * Description: Utility generating zero-padded sequential daily case identifiers.
 * To Implement: Scoped database transaction/lock if deployed in multi-instance active environments.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using ccms_backend.data;

namespace ccms_backend.services;

public class CaseNumberGenerator
{
    private readonly ICaseRepository _caseRepository;

    public CaseNumberGenerator(ICaseRepository caseRepository)
    {
        _caseRepository = caseRepository;
    }

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await _caseRepository.GetTodayCaseCountAsync(ct);
        return $"CCMS-{today}-{(count + 1):D4}";
    }
}
