namespace ccms_backend.models;

public class Defendant
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
}
