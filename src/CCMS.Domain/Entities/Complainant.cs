namespace CCMS.Domain.Entities;

public class Complainant
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
}
