/*
 * File: BankCustomerRepository.cs
 * Description: EF Core repository implementation to look up customer records for matching.
 * To Implement: Filters queries strictly by bankCode to prevent cross-bank queries.
 */

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public class BankCustomerRepository : IBankCustomerRepository
{
    private readonly AppDbContext _context;

    public BankCustomerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BankCustomer?> FindByAccountNumberAsync(string accountNumber, string bankCode, CancellationToken ct = default)
    {
        return await _context.BankCustomers
            .FirstOrDefaultAsync(bc => bc.AccountNumber == accountNumber && bc.BankCode == bankCode, ct);
    }

    public async Task<BankCustomer?> FindByAadhaarAsync(string aadhaar, string bankCode, CancellationToken ct = default)
    {
        return await _context.BankCustomers
            .FirstOrDefaultAsync(bc => bc.AadhaarNumber == aadhaar && bc.BankCode == bankCode, ct);
    }

    public async Task<BankCustomer?> FindByPanAsync(string pan, string bankCode, CancellationToken ct = default)
    {
        return await _context.BankCustomers
            .FirstOrDefaultAsync(bc => bc.PanNumber == pan && bc.BankCode == bankCode, ct);
    }
}
