using CCMS.Domain.Enums;
namespace CCMS.Domain.Entities;

public class BankCustomer
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AadhaarNumber { get; set; } = string.Empty;
    public string PANNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;
    public decimal CurrentBalance { get; set; }
}
