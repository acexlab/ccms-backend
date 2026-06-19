using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using CCMS.Application.DTOs;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using CCMS.Infrastructure.Data;

namespace CCMS.API.Tests.IntegrationTests;

public class EndToEndWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EndToEndWorkflowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullLifecycle_CourtCreatesCase_BatchMatches_BankResponds()
    {
        // ====================================================================
        // STEP 0: Seed Users in In-Memory DB
        // ====================================================================
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new CCMS.Domain.Entities.User
            {
                Username = "court_admin",
                PasswordHash = "hashed_pw",
                Role = CCMS.Domain.Entities.UserRole.Court
            });
            db.Users.Add(new CCMS.Domain.Entities.User
            {
                Username = "bank_admin",
                PasswordHash = "hashed_pw",
                Role = CCMS.Domain.Entities.UserRole.Bank
            });
            await db.SaveChangesAsync();
        }

        // ====================================================================
        // STEP 1: Court Officer Login & Case Creation
        // ====================================================================
        var courtClient = _factory.CreateClient();
        courtClient.DefaultRequestHeaders.Add("X-Test-Username", "court_admin");
        courtClient.DefaultRequestHeaders.Add("X-Test-Role", "Court");

        var createCaseDto = new CreateCaseDto
        {
            ComplainantName = "Income Tax Dept",
            ComplainantId = "ABCDE1234F",
            DefendantName = "End To End Test User",
            DefendantId = "999988887777", // 12 digits so it's matched as Aadhaar
            DefendantAccountNumber = "999888777666",
            DefendantBankName = "SBI",
            OrderType = "FreezeAccount",
            FreezeAmount = 50000
        };

        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(createCaseDto.ComplainantName), "ComplainantName");
        formData.Add(new StringContent(createCaseDto.ComplainantId), "ComplainantId");
        formData.Add(new StringContent(createCaseDto.DefendantName), "DefendantName");
        formData.Add(new StringContent(createCaseDto.DefendantId), "DefendantId");
        formData.Add(new StringContent(createCaseDto.DefendantAccountNumber), "DefendantAccountNumber");
        formData.Add(new StringContent(createCaseDto.DefendantBankName), "DefendantBankName");
        formData.Add(new StringContent(createCaseDto.OrderType), "OrderType");
        formData.Add(new StringContent(createCaseDto.FreezeAmount.ToString() ?? "0"), "FreezeAmount");

        var dummyFile1 = new ByteArrayContent(new byte[] { 1, 2, 3 });
        formData.Add(dummyFile1, "CourtOrderFile", "order.pdf");

        var dummyFile2 = new ByteArrayContent(new byte[] { 1, 2, 3 });
        formData.Add(dummyFile2, "AadhaarFile", "aadhaar.pdf");

        var dummyFile3 = new ByteArrayContent(new byte[] { 1, 2, 3 });
        formData.Add(dummyFile3, "PanFile", "pan.pdf");

        var createResponse = await courtClient.PostAsync("/api/cases", formData);
        var createResponseString = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createResponseString);
        
        // The API returns an anonymous object { caseNumber, id }, so let's extract the caseNumber
        var caseRef = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        var caseNumber = caseRef!["caseNumber"]!.ToString();
        
        var getCaseResponse = await courtClient.GetAsync($"/api/cases/{caseNumber}");
        var createdCase = await getCaseResponse.Content.ReadFromJsonAsync<CaseDetailDto>();
        createdCase.Should().NotBeNull();
        createdCase!.Status.Should().Be("Pending");

        // ====================================================================
        // STEP 2: Pre-seed a Bank Customer so the Batch job finds a match
        // ====================================================================
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BankCustomers.Add(new CCMS.Domain.Entities.BankCustomer
            {
                AccountHolderName = "End To End Test User",
                AadhaarNumber = "999988887777", // Matches DefendantId
                PANNumber = "E2ETESTPAN",
                AccountNumber = "999888777666",
                AccountStatus = CCMS.Domain.Entities.AccountStatus.Active,
                CurrentBalance = 100000
            });
            await db.SaveChangesAsync();
        }

        // ====================================================================
        // STEP 3: Bank Officer Triggers Manual Batch
        // ====================================================================
        var bankClient = _factory.CreateClient();
        bankClient.DefaultRequestHeaders.Add("X-Test-Username", "bank_admin");
        bankClient.DefaultRequestHeaders.Add("X-Test-Role", "Bank");
        bankClient.DefaultRequestHeaders.Add("X-Test-BankCode", "SBI001");

        var batchResponse = await bankClient.PostAsync("/api/batch/run", null);
        batchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify case is now AccountValidated
        var checkCaseResp = await courtClient.GetAsync($"/api/cases/{createdCase.CaseNumber}");
        var validatedCase = await checkCaseResp.Content.ReadFromJsonAsync<CaseDetailDto>();
        validatedCase!.Status.Should().Be("AccountValidated");

        // ====================================================================
        // STEP 4: Bank Officer Submits Response
        // ====================================================================
        var responseDto = new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            Remarks = "Freeze applied successfully as per E2E test",
            FreezeAmountApplied = 50000
        };

        var bankReply = await bankClient.PostAsJsonAsync($"/api/cases/{caseNumber}/response", responseDto);
        bankReply.StatusCode.Should().Be(HttpStatusCode.OK);

        // ====================================================================
        // STEP 5: Final Verification - Case is Responded
        // ====================================================================
        var finalCheck = await courtClient.GetAsync($"/api/cases/{createdCase.CaseNumber}");
        var finalCase = await finalCheck.Content.ReadFromJsonAsync<CaseDetailDto>();
        
        finalCase!.Status.Should().Be("FreezeApplied");
        finalCase.Response.Should().NotBeNull();
        finalCase.Response!.Remarks.Should().Contain("E2E test");
    }
}
