using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CCMS.Application.Interfaces;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Application.Services;

public class BatchValidationService
{
    private readonly IBatchJobLogRepository _batchLogRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly IAppDbContext _context;
    private readonly ILogger<BatchValidationService> _logger;

    public BatchValidationService(
        IBatchJobLogRepository batchLogRepo,
        ICaseRepository caseRepo,
        IAppDbContext context,
        ILogger<BatchValidationService> logger)
    {
        _batchLogRepo = batchLogRepo;
        _caseRepo = caseRepo;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Manually triggered batch run (called by Bank Officer from dashboard).
    /// </summary>
    public async Task<BatchJobLog> TriggerManualRunAsync(int? userId)
    {
        return await RunBatchAsync(TriggeredBy.Manual, userId);
    }

    /// <summary>
    /// Scheduled batch run (called by the background scheduler).
    /// </summary>
    public async Task<BatchJobLog> TriggerBatchRunAsync(TriggeredBy triggeredBy, int? userId)
    {
        return await RunBatchAsync(triggeredBy, userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core batch logic
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<BatchJobLog> RunBatchAsync(TriggeredBy triggeredBy, int? userId)
    {
        var runId = $"BATCH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

        // Create and persist the running log entry immediately
        var log = await _batchLogRepo.AddLogAsync(new BatchJobLog
        {
            RunId = runId,
            TriggeredBy = triggeredBy,
            TriggeredByUserId = userId,
            StartTime = DateTime.UtcNow,
            Status = BatchJobStatus.Running
        });

        _logger.LogInformation("Batch run {RunId} started (triggered by {TriggeredBy}).", runId, triggeredBy);

        int matchedCount = 0;
        int notFoundCount = 0;

        try
        {
            // ── IMPORTANT: only process cases that are currently Pending ──────
            // Cases in any other state (AccountValidated, AccountNotFound,
            // FreezeApplied, BalanceProvided) must be left completely untouched.
            var pendingCases = await _context.Cases
                .Include(c => c.Defendant)
                .Where(c => c.Status == CaseStatus.Pending)
                .ToListAsync();

            _logger.LogInformation("Batch run {RunId}: found {Count} pending case(s) to process.", runId, pendingCases.Count);

            foreach (var courtCase in pendingCases)
            {
                var defendant = courtCase.Defendant;

                if (defendant == null)
                {
                    // No defendant data at all — auto-close
                    MarkNotFound(courtCase, "No defendant information available for this case.");
                    notFoundCount++;
                    continue;
                }

                // ── Sequential matching: Account Number → Aadhaar → PAN ────────
                var (matchedCustomer, matchedOn) = await FindMatchingCustomerAsync(defendant);

                if (matchedCustomer != null)
                {
                    // Match found — mark AccountValidated and record details
                    courtCase.Status = CaseStatus.AccountValidated;
                    courtCase.UpdatedAt = DateTime.UtcNow;

                    _context.CaseValidationResults.Add(new CaseValidationResult
                    {
                        CaseId = courtCase.Id,
                        MatchedAccountNumber = matchedCustomer.AccountNumber,
                        AccountHolderName = matchedCustomer.AccountHolderName,
                        AccountStatus = matchedCustomer.AccountStatus.ToString(),
                        CurrentBalance = matchedCustomer.CurrentBalance,
                        MatchedOn = matchedOn!.Value,
                        ValidatedAt = DateTime.UtcNow
                    });

                    matchedCount++;
                    _logger.LogInformation(
                        "Batch run {RunId}: case {CaseNumber} matched customer '{Name}' by {MatchedOn}.",
                        runId, courtCase.CaseNumber, matchedCustomer.AccountHolderName, matchedOn);
                }
                else
                {
                    // No match found — auto-close with AccountNotFound response
                    MarkNotFound(
                        courtCase,
                        "System Auto-Response: Defendant bank account could not be found matching the provided details.");
                    notFoundCount++;
                    _logger.LogInformation(
                        "Batch run {RunId}: case {CaseNumber} — no matching account found.",
                        runId, courtCase.CaseNumber);
                }
            }

            // Persist all changes (case statuses, validation results, auto-responses)
            await _context.SaveChangesAsync();

            // Finalise log
            log.EndTime = DateTime.UtcNow;
            log.CasesProcessed = pendingCases.Count;
            log.AccountsMatched = matchedCount;
            log.AccountsNotFound = notFoundCount;
            log.DurationSeconds = (int)(log.EndTime.Value - log.StartTime).TotalSeconds;
            log.Status = BatchJobStatus.Success;

            _logger.LogInformation(
                "Batch run {RunId} completed: {Total} processed, {Matched} matched, {NotFound} not found. Duration: {Duration}s.",
                runId, pendingCases.Count, matchedCount, notFoundCount, log.DurationSeconds);
        }
        catch (Exception ex)
        {
            log.EndTime = DateTime.UtcNow;
            log.Status = BatchJobStatus.Failed;
            log.DurationSeconds = (int)(log.EndTime.Value - log.StartTime).TotalSeconds;
            _logger.LogError(ex, "Batch run {RunId} failed with exception.", runId);
        }

        await _context.SaveChangesAsync();
        return log;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sequential customer lookup
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to find a matching BankCustomer using three criteria in order:
    ///   1. Account Number (exact match)
    ///   2. Aadhaar Number (exact match after normalisation)
    ///   3. PAN Number (case-insensitive exact match)
    ///
    /// Each criterion is only tried if the previous one found no match.
    /// Returns (null, null) when no match is found after all three attempts.
    /// </summary>
    private async Task<(BankCustomer? Customer, MatchedOn? MatchedOn)> FindMatchingCustomerAsync(Defendant defendant)
    {
        // 1. Account Number
        if (!string.IsNullOrWhiteSpace(defendant.BankAccountNumber))
        {
            var customer = await _context.BankCustomers
                .FirstOrDefaultAsync(bc => bc.AccountNumber == defendant.BankAccountNumber.Trim());

            if (customer != null)
                return (customer, MatchedOn.AccountNumber);
        }

        // 2. Aadhaar (12-digit, normalised — remove spaces/hyphens)
        if (!string.IsNullOrWhiteSpace(defendant.IdentityNumber))
        {
            var cleanAadhaar = NormaliseAadhaar(defendant.IdentityNumber);
            if (cleanAadhaar.Length == 12)
            {
                var customer = await _context.BankCustomers
                    .FirstOrDefaultAsync(bc =>
                        bc.AadhaarNumber.Replace("-", "").Replace(" ", "") == cleanAadhaar);

                if (customer != null)
                    return (customer, MatchedOn.Aadhaar);
            }
        }

        // 3. PAN Number
        if (!string.IsNullOrWhiteSpace(defendant.PanNumber))
        {
            var pan = defendant.PanNumber.Trim().ToUpperInvariant();
            var customer = await _context.BankCustomers
                .FirstOrDefaultAsync(bc => bc.PANNumber.ToUpper() == pan);

            if (customer != null)
                return (customer, MatchedOn.PAN);
        }

        return (null, null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Removes spaces and hyphens from an Aadhaar string.</summary>
    private static string NormaliseAadhaar(string raw)
        => raw.Replace("-", "").Replace(" ", "").Trim();

    /// <summary>
    /// Marks a case as AccountNotFound and records an automatic system response.
    /// </summary>
    private void MarkNotFound(Case courtCase, string remarks)
    {
        courtCase.Status = CaseStatus.AccountNotFound;
        courtCase.UpdatedAt = DateTime.UtcNow;

        _context.CaseResponses.Add(new CaseResponse
        {
            CaseId = courtCase.Id,
            ResponseType = ResponseType.AccountNotFound,
            Remarks = remarks,
            SubmittedAt = DateTime.UtcNow
        });
    }
}
