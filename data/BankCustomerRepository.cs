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

    public async Task<BankCustomer?> GetByAccountNumberAsync(string accountNumber)
    {
        return await _context.BankCustomers.FirstOrDefaultAsync(bc => bc.AccountNumber == accountNumber);
    }

    public async Task<BankCustomer?> GetByAadhaarAsync(string aadhaar)
    {
        var cleaned = aadhaar.Replace("-", "").Replace(" ", "");
        return await _context.BankCustomers.FirstOrDefaultAsync(bc => bc.AadhaarNumber.Replace("-", "").Replace(" ", "") == cleaned);
    }

    public async Task<BankCustomer?> GetByPanAsync(string pan)
    {
        var cleaned = pan.ToUpper().Trim();
        return await _context.BankCustomers.FirstOrDefaultAsync(bc => bc.PANNumber.ToUpper() == cleaned);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}