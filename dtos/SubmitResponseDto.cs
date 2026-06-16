namespace ccms_backend.dtos;

public class SubmitResponseDto
{
    public string ResponseType { get; set; } = string.Empty;
    public decimal? FreezeAmountApplied { get; set; }
    public decimal? BalanceReported { get; set; }
    public string? Remarks { get; set; }
}