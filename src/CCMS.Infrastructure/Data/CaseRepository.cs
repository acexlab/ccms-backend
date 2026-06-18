using CCMS.Application.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Infrastructure.Data;

public class CaseRepository : ICaseRepository
{
    private readonly AppDbContext _context;

    public CaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Case>> GetCasesForBankAsync()
    {
        return await _context.Cases
            .Include(c => c.Defendant)
            .Include(c => c.Complainant)
            .ToListAsync();
    }
}
