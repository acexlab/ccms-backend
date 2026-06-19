using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;
using CCMS.Application.DTOs;

namespace CCMS.API.Tests.IntegrationTests;

public class ResponsesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ResponsesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubmitResponse_ShouldReturn409Conflict_WhenCaseIsAlreadyInTerminalState()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "bank_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Bank");

        int caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Ensure users are present
            if (!context.Users.Any(u => u.Username == "bank_officer"))
            {
                context.Users.Add(new User { Id = 2, Username = "bank_officer", Role = UserRole.Bank, PasswordHash = "" });
            }

            // Seed a case that has already received a response and is in a terminal state (FreezeApplied)
            var terminalCase = new Case
            {
                CaseNumber = "CCMS-20260616-1212",
                OrderType = OrderType.FreezeAccount,
                Status = CaseStatus.FreezeApplied,
                CreatedByUserId = 1
            };
            context.Cases.Add(terminalCase);
            await context.SaveChangesAsync();
            caseId = terminalCase.Id;

            // Seed a corresponding CaseResponse
            var existingResponse = new CaseResponse
            {
                CaseId = caseId,
                RespondedByUserId = 2,
                ResponseType = ResponseType.FreezeApplied,
                FreezeAmountApplied = 5000.00m,
                Remarks = "Original Response",
                SubmittedAt = System.DateTime.UtcNow
            };
            context.CaseResponses.Add(existingResponse);
            await context.SaveChangesAsync();
        }

        var duplicateDto = new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            FreezeAmountApplied = 6000.00m,
            Remarks = "Duplicate Attempt"
        };

        // Act
        var response = await client.PostAsync($"/api/cases/CCMS-20260616-1212/response", JsonContent.Create(duplicateDto));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("A response already exists for this case.");
    }
}
