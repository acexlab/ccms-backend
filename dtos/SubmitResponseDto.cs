/*
 * File: SubmitResponseDto.cs
 * Description: Request body payload for bank officers to submit case response feedback.
 * To Implement: Keep in sync with CaseResponse entity.
 */

namespace ccms_backend.dtos;

public class SubmitResponseDto
{
    public decimal? ReportedAmount { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
