using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ccms_backend.dtos;
using ccms_backend.services;

namespace ccms_backend.controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Bank")]
public class CasesController : ControllerBase
{
    private readonly CaseService _caseService;

    public CasesController(CaseService caseService)
    {
        _caseService = caseService;
    }

    [HttpGet("bank")]
    public async Task<ActionResult<IEnumerable<CaseSummaryDto>>> GetCasesForBank()
    {
        var cases = await _caseService.GetCasesForBankAsync();
        return Ok(cases);
    }
}