/*
 * File: CreateCaseDto.cs
 * Description: Data transfer object containing request parameters for creating a new Case.
 * To Implement: The file uploads themselves are mapped to streams in controllers.
 */

using System.ComponentModel.DataAnnotations;

namespace CCMS.Application.DTOs;

public class CreateCaseDto
{
    [Required]
    public string ComplainantName { get; set; } = string.Empty;

    [Required]
    public string ComplainantId { get; set; } = string.Empty;

    [Required]
    public string DefendantName { get; set; } = string.Empty;

    [Required]
    public string DefendantId { get; set; } = string.Empty;

    [Required]
    public string DefendantAccountNumber { get; set; } = string.Empty;

    [Required]
    public string DefendantBankName { get; set; } = string.Empty;

    [Required]
    public string OrderType { get; set; } = string.Empty; // "FreezeAccount" | "BalanceEnquiry"

    public decimal? FreezeAmount { get; set; }
}
