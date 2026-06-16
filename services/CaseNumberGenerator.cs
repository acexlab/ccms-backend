/*
 * File: CaseNumberGenerator.cs
 * Description: Utility generating zero-padded sequential daily case identifiers.
 * To Implement: Scoped database transaction/lock if deployed in multi-instance active environments.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ccms_backend.data;

namespace ccms_backend.services;

public class CaseNumberGenerator
{
    private readonly AppDbContext _context;

    public CaseNumberGenerator(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var count = await _context.Cases.CountAsync(c => c.CreatedAt >= today, ct);
        int nextSequence = count + 1;
        return $"CCMS-{DateTime.UtcNow:yyyyMMdd}-{nextSequence:D4}";
    }
}