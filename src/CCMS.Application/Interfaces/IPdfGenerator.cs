using CCMS.Application.DTOs;

namespace CCMS.Application.Interfaces;

public interface IPdfGenerator
{
    byte[] GenerateCourtOrder(CreateCaseDto dto, string caseNumber);
    byte[] GenerateSimulatedPdf(string fileName);
}
