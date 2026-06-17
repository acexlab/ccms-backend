using CCMS.Application.Interfaces;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CCMS.Application.DTOs;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using System.Linq;
using System;

namespace CCMS.API.Controllers;

[ApiController]
[Route("api/bank/dashboard")]
[Authorize(Roles = "Bank")]
public class BankDashboardController : ControllerBase
{
    private readonly ICaseRepository _caseRepository;
    private readonly IBatchJobLogRepository _batchJobLogRepository;

    public BankDashboardController(ICaseRepository caseRepository, IBatchJobLogRepository batchJobLogRepository)
    {
        _caseRepository = caseRepository;
        _batchJobLogRepository = batchJobLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<BankDashboardResponseDto>> GetDashboard()
    {
        var cases = await _caseRepository.GetCasesForBankAsync();
        var lastRun = await _batchJobLogRepository.GetLastRunAsync();

        var pending = cases.Count(c => c.Status == CaseStatus.Pending);
        var validated = cases.Count(c => c.Status == CaseStatus.AccountValidated);
        var notFound = cases.Count(c => c.Status == CaseStatus.AccountNotFound);
        var freezeApplied = cases.Count(c => c.Status == CaseStatus.FreezeApplied);
        var balanceProvided = cases.Count(c => c.Status == CaseStatus.BalanceProvided);

        var freezeOrders = cases.Count(c => c.OrderType == OrderType.FreezeAccount);
        var balanceOrders = cases.Count(c => c.OrderType == OrderType.BalanceEnquiry);

        string durationStr = "00:00:00";
        if (lastRun != null && lastRun.DurationSeconds.HasValue)
        {
            var ts = TimeSpan.FromSeconds(lastRun.DurationSeconds.Value);
            durationStr = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        return Ok(new BankDashboardResponseDto
        {
            Pending = pending,
            AccountValidated = validated,
            AccountNotFound = notFound,
            FreezeApplied = freezeApplied,
            BalanceProvided = balanceProvided,
            LastRunTime = lastRun?.StartTime,
            Duration = durationStr,
            FreezeOrders = freezeOrders,
            BalanceOrders = balanceOrders
        });
    }
}
