/*
 * File: IBankCustomerRepository.cs
 * Description: Interface defining data queries to check customer records for batch matching.
 * To Implement: Keep in sync with BankCustomer database configurations.
 */

using System.Threading;
using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface IBankCustomerRepository
{
    Task<BankCustomer?> FindByAccountNumberAsync(string accountNumber, string bankCode, CancellationToken ct = default);
    Task<BankCustomer?> FindByAadhaarAsync(string aadhaar, string bankCode, CancellationToken ct = default);
    Task<BankCustomer?> FindByPanAsync(string pan, string bankCode, CancellationToken ct = default);
}
