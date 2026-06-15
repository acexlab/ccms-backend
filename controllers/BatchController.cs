using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ccms_backend.models;
using ccms_backend.services;
using ccms_backend.data;
using System.Security.Claims;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Bank")]
public class BatchController : ControllerBase
{
    private readonly BatchValidationService _batchValidationService;
    private readonly IBatchJobLogRepository _batchJobLogRepository;

    public BatchController(BatchValidationService batchValidationService, IBatchJobLogRepository batchJobLogRepository)
    {
        _batchValidationService = batchValidationService;
        _batchJobLogRepository = batchJobLogRepository;
    }

    [HttpGet("last-run")]
    public async Task<ActionResult<BatchJobLog>> GetLastRun()
    {
        var lastRun = await _batchJobLogRepository.GetLastRunAsync();
        if (lastRun == null)
            return NotFound(new { Message = "No batch runs found." });
            
        return Ok(lastRun);
    }

    [HttpPost("run")]
    public async Task<ActionResult> RunManualBatch()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = null;
        if (int.TryParse(userIdString, out int id))
        {
            userId = id;
        }

        await _batchValidationService.TriggerManualRunAsync(userId);
        return Ok(new { success = true, message = "Batch Job Started Successfully" });
    }
}