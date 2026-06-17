namespace CCMS.Application.DTOs;

public class BankDashboardResponseDto
{
    public int Pending { get; set; }
    public int AccountValidated { get; set; }
    public int AccountNotFound { get; set; }
    public int FreezeApplied { get; set; }
    public int BalanceProvided { get; set; }
    public DateTime? LastRunTime { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int FreezeOrders { get; set; }
    public int BalanceOrders { get; set; }
}
