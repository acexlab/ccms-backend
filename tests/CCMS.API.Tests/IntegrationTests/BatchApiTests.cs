using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.API.Tests.IntegrationTests;

public class BatchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BatchApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RunManualBatch_ShouldTriggerBatchExecution_AndLogOutcome()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "bank_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Bank");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // Clean log table
            context.BatchJobLogs.RemoveRange(context.BatchJobLogs);
            
            // Seed a pending case so the batch has work to validate
            var pendingCase = new Case
            {
                CaseNumber = "CCMS-20260616-7777",
                OrderType = OrderType.FreezeAccount,
                Status = CaseStatus.Pending,
                CreatedByUserId = 1,
                Defendant = new Defendant
                {
                    FullName = "John Smith",
                    BankAccountNumber = "9876543210"
                }
            };
            context.Cases.Add(pendingCase);
            await context.SaveChangesAsync();
        }

        // Act
        var response = await client.PostAsync("/api/batch/run", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that database log was written
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logs = context.BatchJobLogs.ToList();
            logs.Should().NotBeEmpty();
            
            var lastLog = logs.OrderByDescending(l => l.StartTime).First();
            lastLog.Status.Should().Be(BatchJobStatus.Success);
            lastLog.CasesProcessed.Should().BeGreaterThan(0);
        }
    }
}
