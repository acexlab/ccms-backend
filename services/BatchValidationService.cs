using System;
using System.Linq;
using System.Threading.Tasks;
using ccms_backend.data;
using ccms_backend.models;

namespace ccms_backend.services;

public class BatchValidationService
{
    private readonly IBatchJobLogRepository _batchLogRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly AppDbContext _context;

    public BatchValidationService(IBatchJobLogRepository batchLogRepo, ICaseRepository caseRepo, AppDbContext context)
    {
        _batchLogRepo = batchLogRepo;
        _caseRepo = caseRepo;
        _context = context;
    }

    public async Task<BatchJobLog> TriggerManualRunAsync(int? userId)
    {
        var log = new BatchJobLog
        {
            RunId = Guid.NewGuid().ToString("N"),
            TriggeredBy = TriggeredBy.Manual,
            TriggeredByUserId = userId,
            StartTime = DateTime.UtcNow,
            Status = BatchJobStatus.Running
        };

        log = await _batchLogRepo.AddLogAsync(log);

        // MOCKED VALIDATION PASS
        var allCases = await _caseRepo.GetCasesForBankAsync();
        var pendingCases = allCases.Where(c => c.Status == CaseStatus.Pending).ToList();

        int matchedCount = 0;
        int notFoundCount = 0;
        var random = new Random();

        foreach (var c in pendingCases)
        {
            // Mock logic: 80% match, 20% not found
            bool isMatch = random.NextDouble() < 0.8;
            if (isMatch)
            {
                c.Status = CaseStatus.AccountValidated;
                matchedCount++;
            }
            else
            {
                c.Status = CaseStatus.AccountNotFound;
                notFoundCount++;
            }
        }

        await _context.SaveChangesAsync();

        // Finalize log
        log.EndTime = DateTime.UtcNow;
        log.CasesProcessed = pendingCases.Count;
        log.AccountsMatched = matchedCount;
        log.AccountsNotFound = notFoundCount;
        log.DurationSeconds = (int)(log.EndTime.Value - log.StartTime).TotalSeconds;
        log.Status = BatchJobStatus.Success;

        await _context.SaveChangesAsync();

        return log;
    }
}