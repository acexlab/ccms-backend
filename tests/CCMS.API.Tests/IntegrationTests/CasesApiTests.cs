using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.API.Tests.IntegrationTests;

public class CasesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CasesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private void SeedUsers(AppDbContext context)
    {
        // Seed users to ensure foreign keys match
        if (!context.Users.Any(u => u.Username == "court_officer"))
        {
            context.Users.Add(new User { Id = 1, Username = "court_officer", Role = UserRole.Court, PasswordHash = "" });
        }
        if (!context.Users.Any(u => u.Username == "bank_officer"))
        {
            context.Users.Add(new User { Id = 2, Username = "bank_officer", Role = UserRole.Bank, PasswordHash = "" });
        }
        context.SaveChanges();
    }

    [Fact]
    public async Task CreateCase_ShouldSucceed_WhenValidRequestAndAllFilesUploaded()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "court_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Court");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedUsers(context);
        }

        var boundary = Guid.NewGuid().ToString();
        using var content = new MultipartFormDataContent(boundary);

        content.Add(new StringContent("State Authority"), "ComplainantName");
        content.Add(new StringContent("123456789012"), "ComplainantId");
        content.Add(new StringContent("John Smith"), "DefendantName");
        content.Add(new StringContent("123456789012"), "DefendantId");
        content.Add(new StringContent("ABCDE1234F"), "DefendantPan");
        content.Add(new StringContent("9876543210"), "DefendantAccountNumber");
        content.Add(new StringContent("WEST"), "DefendantBankName");
        content.Add(new StringContent("FreezeAccount"), "OrderType");
        content.Add(new StringContent("15000.00"), "FreezeAmount");

        // Add mock files
        var pdfBytes = new byte[1024]; // 1KB mock file
        var pngBytes = new byte[1024];

        var courtOrderContent = new ByteArrayContent(pdfBytes);
        courtOrderContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(courtOrderContent, "courtOrderFile", "court_order.pdf");

        var aadhaarContent = new ByteArrayContent(pngBytes);
        aadhaarContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(aadhaarContent, "aadhaarFile", "aadhaar.png");

        var panContent = new ByteArrayContent(pngBytes);
        panContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(panContent, "panFile", "pan.png");

        // Act
        var response = await client.PostAsync("/api/cases", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseBody);
        json.RootElement.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        json.RootElement.GetProperty("caseNumber").GetString().Should().StartWith("CCMS-");
    }

    [Fact]
    public async Task CreateCase_ShouldFailWithBadRequest_WhenFilesAreMissing()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "court_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Court");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedUsers(context);
        }

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("State Authority"), "ComplainantName");
        content.Add(new StringContent("123456789012"), "ComplainantId");
        content.Add(new StringContent("John Smith"), "DefendantName");
        content.Add(new StringContent("123456789012"), "DefendantId");
        content.Add(new StringContent("ABCDE1234F"), "DefendantPan");
        content.Add(new StringContent("9876543210"), "DefendantAccountNumber");
        content.Add(new StringContent("WEST"), "DefendantBankName");

        // Missing file uploads (e.g. panFile and aadhaarFile missing)
        var pdfBytes = new byte[1024];
        var courtOrderContent = new ByteArrayContent(pdfBytes);
        courtOrderContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(courtOrderContent, "courtOrderFile", "court_order.pdf");

        // Act
        var response = await client.PostAsync("/api/cases", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCaseById_ShouldReturnMaskedIdentityAndBankAccountNumbers()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "bank_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Bank");

        int caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedUsers(context);

            var @case = new Case
            {
                CaseNumber = "CCMS-20260616-9999",
                OrderType = OrderType.FreezeAccount,
                Status = CaseStatus.AccountValidated,
                CreatedByUserId = 1,
                Defendant = new Defendant
                {
                    FullName = "Alexander Vance",
                    BankAccountNumber = "50200004558291",
                    IdentityNumber = "1234-5678-9012",
                    PanNumber = "ABCDE1234F"
                }
            };
            context.Cases.Add(@case);
            await context.SaveChangesAsync();
            caseId = @case.Id;
        }

        // Act
        var response = await client.GetAsync($"/api/cases/{caseId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseBody);
        
        var defendant = json.RootElement.GetProperty("defendant");
        
        // Assert masked fields match expectations
        defendant.GetProperty("identityNumber").GetString().Should().Be("****-****-9012");
        defendant.GetProperty("panNumber").GetString().Should().Be("******234F");
        defendant.GetProperty("bankAccountNumber").GetString().Should().Be("**********8291");
    }
}
