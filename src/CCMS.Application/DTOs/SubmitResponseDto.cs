namespace CCMS.Application.DTOs;

public class SubmitResponseDto
{
    public string ResponseType { get; set; } = string.Empty; // "FreezeApplied", "BalanceProvided", "AccountNotFound"
    public decimal? FreezeAmountApplied { get; set; }
    public decimal? BalanceReported { get; set; }
    public string? Remarks { get; set; }
}
