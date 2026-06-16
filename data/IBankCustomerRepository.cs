using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface IBankCustomerRepository
{
    Task<BankCustomer?> GetByAccountNumberAsync(string accountNumber);
    Task<BankCustomer?> GetByAadhaarAsync(string aadhaar);
    Task<BankCustomer?> GetByPanAsync(string pan);
    Task SaveChangesAsync();
}