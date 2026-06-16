using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ccms_backend.data;
using ccms_backend.models;

namespace ccms_backend.services;

public class BatchValidationService
{
    private readonly IBatchJobLogRepository _batchLogRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly AppDbContext _context;
    private readonly IBankCustomerRepository _bankCustomerRepository;

    public BatchValidationService(IBatchJobLogRepository batchLogRepo, ICaseRepository caseRepo, AppDbContext context, IBankCustomerRepository bankCustomerRepository)
    {
        _batchLogRepo = batchLogRepo;
        _caseRepo = caseRepo;
        _context = context;
        _bankCustomerRepository = bankCustomerRepository;
    }

    public async Task<BatchJobLog> TriggerManualRunAsync(int? userId)
    {
        return await TriggerBatchRunAsync(TriggeredBy.Manual, userId);
    }

    public async Task<BatchJobLog> TriggerBatchRunAsync(TriggeredBy triggeredBy, int? userId)
    {
        var runId = $"BATCH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
        var log = new BatchJobLog
        {
            RunId = runId,
            TriggeredBy = triggeredBy,
            TriggeredByUserId = userId,
            StartTime = DateTime.UtcNow,
            Status = BatchJobStatus.Running
        };

        log = await _batchLogRepo.AddLogAsync(log);

        // Fetch only cases that are currently in the Pending state
        var pendingCases = await _context.Cases
            .Include(c => c.Defendant)
            .Where(c => c.Status == CaseStatus.Pending)
            .ToListAsync();

        int matchedCount = 0;
        int notFoundCount = 0;

        foreach (var c in pendingCases)
        {
            if (c.Defendant == null)
            {
                c.Status = CaseStatus.AccountNotFound;
                c.UpdatedAt = DateTime.UtcNow;
                notFoundCount++;
                continue;
            }

            var def = c.Defendant;
            BankCustomer? matchedCustomer = null;
            MatchedOn? matchedOn = null;

            // 1. Match by Account Number (only if provided)
            if (!string.IsNullOrWhiteSpace(def.BankAccountNumber))
            {
                var accountNum = def.BankAccountNumber.Trim();
                matchedCustomer = await _bankCustomerRepository.GetByAccountNumberAsync(accountNum);
                
                if (matchedCustomer != null)
                {
                    matchedOn = MatchedOn.AccountNumber;
                }
            }

            // 2. Match by Aadhaar (if Account Number match failed)
            if (matchedCustomer == null && !string.IsNullOrWhiteSpace(def.IdentityNumber))
            {
                // Regex matches standard 12-digit Indian Aadhaar pattern (possibly separated by space or hyphen)
                var aadhaarMatch = Regex.Match(def.IdentityNumber, @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}\b");
                if (aadhaarMatch.Success)
                {
                    var cleanedAadhaar = aadhaarMatch.Value.Replace("-", "").Replace(" ", "");
                    matchedCustomer = await _bankCustomerRepository.GetByAadhaarAsync(cleanedAadhaar);
                    
                    if (matchedCustomer != null)
                    {
                        matchedOn = MatchedOn.Aadhaar;
                    }
                }
            }

            // 3. Match by PAN (if Aadhaar match failed)
            if (matchedCustomer == null && !string.IsNullOrWhiteSpace(def.IdentityNumber))
            {
                // Regex matches standard Indian PAN format: 5 letters, 4 digits, 1 letter
                var panMatch = Regex.Match(def.IdentityNumber, @"\b[A-Za-z]{5}\d{4}[A-Za-z]\b");
                if (panMatch.Success)
                {
                    var cleanedPan = panMatch.Value.ToUpper();
                    matchedCustomer = await _bankCustomerRepository.GetByPanAsync(cleanedPan);
                    
                    if (matchedCustomer != null)
                    {
                        matchedOn = MatchedOn.PAN;
                    }
                }
            }

            // Determine outcome
            if (matchedCustomer != null)
            {
                c.Status = CaseStatus.AccountValidated;
                c.UpdatedAt = DateTime.UtcNow;

                // Add CaseValidationResult record
                var validationResult = new CaseValidationResult
                {
                    CaseId = c.Id,
                    MatchedAccountNumber = matchedCustomer.AccountNumber,
                    AccountHolderName = matchedCustomer.AccountHolderName,
                    AccountStatus = matchedCustomer.AccountStatus.ToString(),
                    CurrentBalance = matchedCustomer.AvailableBalance,
                    MatchedOn = matchedOn!.Value,
                    ValidatedAt = DateTime.UtcNow
                };
                _context.CaseValidationResults.Add(validationResult);

                matchedCount++;
            }
            else
            {
                c.Status = CaseStatus.AccountNotFound;
                c.UpdatedAt = DateTime.UtcNow;

                var systemResponse = new CaseResponse
                {
                    CaseId = c.Id,
                    ResponseType = ResponseType.AccountNotFound,
                    Remarks = "No matching account found in bank records",
                    SubmittedAt = DateTime.UtcNow
                };
                _context.CaseResponses.Add(systemResponse);
                
                notFoundCount++;
            }
        }

        // Save modifications to cases & new validation entries
        await _context.SaveChangesAsync();

        // Finalize execution log
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