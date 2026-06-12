/*
 * File: BatchValidationService.cs
 * Description: Services executing automated defendant matching logic.
 * To Implement: Matches via Account Number -> Aadhaar -> PAN fallback.
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;

namespace ccms_backend.services;

public class BatchValidationService
{
    private readonly ICaseRepository _caseRepository;
    private readonly IBankCustomerRepository _bankCustomerRepository;
    private readonly IBatchJobLogRepository _batchJobLogRepository;

    public BatchValidationService(
        ICaseRepository caseRepository,
        IBankCustomerRepository bankCustomerRepository,
        IBatchJobLogRepository batchJobLogRepository)
    {
        _caseRepository = caseRepository;
        _bankCustomerRepository = bankCustomerRepository;
        _batchJobLogRepository = batchJobLogRepository;
    }

    public async Task<BatchJobLogDto> RunBatchValidationAsync(bool isManualTrigger, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var pendingCases = await _caseRepository.GetByStatusAsync(CaseStatus.Pending, ct);

        int processedCount = 0;
        int validatedCount = 0;
        int notFoundCount = 0;
        string? errorMessage = null;

        try
        {
            foreach (var @case in pendingCases)
            {
                processedCount++;
                BankCustomer? matchedCustomer = null;

                // Match Strategy:
                // 1. Account Number
                if (!string.IsNullOrEmpty(@case.DefendantAccountNumber))
                {
                    matchedCustomer = await _bankCustomerRepository.FindByAccountNumberAsync(@case.DefendantAccountNumber, @case.BankCode, ct);
                }

                // 2. Fallback to Aadhaar
                if (matchedCustomer == null && !string.IsNullOrEmpty(@case.DefendantAadhaar))
                {
                    matchedCustomer = await _bankCustomerRepository.FindByAadhaarAsync(@case.DefendantAadhaar, @case.BankCode, ct);
                }

                // 3. Fallback to PAN
                if (matchedCustomer == null && !string.IsNullOrEmpty(@case.DefendantPan))
                {
                    matchedCustomer = await _bankCustomerRepository.FindByPanAsync(@case.DefendantPan, @case.BankCode, ct);
                }

                if (matchedCustomer != null)
                {
                    // Match Found
                    @case.Status = CaseStatus.AccountValidated;
                    @case.MatchedAccountNumber = matchedCustomer.AccountNumber;
                    @case.MatchedBalance = matchedCustomer.Balance;
                    @case.MatchedAccountStatus = matchedCustomer.AccountStatus;

                    validatedCount++;
                }
                else
                {
                    // No Match Found - Auto Resolve with AccountNotFound
                    @case.Status = CaseStatus.AccountNotFound;
                    @case.Response = new CaseResponse
                    {
                        ResponseType = "AccountNotFound",
                        Remarks = "No matching account found in bank records.",
                        IsSystemGenerated = true,
                        RespondedAt = DateTime.UtcNow
                    };

                    notFoundCount++;
                }

                await _caseRepository.UpdateAsync(@case, ct);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var log = new BatchJobLog
            {
                RunAt = DateTime.UtcNow,
                IsManualTrigger = isManualTrigger,
                CasesProcessed = processedCount,
                CasesValidated = validatedCount,
                CasesNotFound = notFoundCount,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = errorMessage
            };

            await _batchJobLogRepository.AddAsync(log, ct);
        }

        return new BatchJobLogDto
        {
            RunAt = DateTime.UtcNow,
            IsManualTrigger = isManualTrigger,
            CasesProcessed = processedCount,
            CasesValidated = validatedCount,
            CasesNotFound = notFoundCount,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
