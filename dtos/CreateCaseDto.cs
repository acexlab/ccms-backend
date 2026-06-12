/*
 * File: CreateCaseDto.cs
 * Description: Data transfer object containing request parameters for creating a new Case.
 * To Implement: The file uploads themselves are mapped to streams in controllers.
 */

namespace ccms_backend.dtos;

public class CreateCaseDto
{
    public string ComplainantName { get; set; } = string.Empty;
    public string DefendantName { get; set; } = string.Empty;
    public string DefendantAadhaar { get; set; } = string.Empty;
    public string DefendantPan { get; set; } = string.Empty;
    public string DefendantAccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty; // "FreezeAccount" | "BalanceEnquiry"
    public decimal? FreezeAmount { get; set; }
}
