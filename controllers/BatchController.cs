/*
 * File: BatchController.cs
 * Description: Controller managing manual batch executions and logs (restricted to Bank Officers).
 * To Implement: Keep endpoint secure via BankOfficer role check.
 */

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.services;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "BankOfficer")]
public class BatchController : ControllerBase
{
    private readonly BatchValidationService _batchService;
    private readonly IBatchJobLogRepository _logRepository;

    public BatchController(BatchValidationService batchService, IBatchJobLogRepository logRepository)
    {
        _batchService = batchService;
        _logRepository = logRepository;
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunBatch()
    {
        var result = await _batchService.RunBatchValidationAsync(isManualTrigger: true, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("last-run")]
    public async Task<IActionResult> GetLastRun()
    {
        var log = await _logRepository.GetLastRunAsync(HttpContext.RequestAborted);
        if (log == null)
        {
            return NotFound("No batch job runs found.");
        }

        var dto = new BatchJobLogDto
        {
            RunAt = log.RunAt,
            IsManualTrigger = log.IsManualTrigger,
            CasesProcessed = log.CasesProcessed,
            CasesValidated = log.CasesValidated,
            CasesNotFound = log.CasesNotFound,
            DurationMs = log.DurationMs
        };

        return Ok(dto);
    }
}
// Note: Allows manual bypass of scheduled BackgroundService.
