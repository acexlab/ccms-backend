using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ccms_backend.data;
using ccms_backend.dtos;
using ccms_backend.models;

namespace CCMS.IntegrationTests;

public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> LoginAndGetTokenAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        return result.Token;
    }

    private string GetExpiredToken()
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretJWTKey12345678901234567890"));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "CCMS.API",
            audience: "CCMS.Client",
            claims: new[]
            {
                new Claim("unique_name", "court.user"),
                new Claim("role", "Court"),
                new Claim(ClaimTypes.NameIdentifier, "1")
            },
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: creds
        );
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    // --- Authentication Tests ---

    [Fact]
    public async Task Test_Login_Success_ReturnsTokenAndRedirectUrl()
    {
        await _factory.ResetDatabaseAsync();

        // 1. Court User Login
        var client = _factory.CreateClient();
        var responseCourt = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = "court.user",
            Password = "Password@123"
        });
        Assert.Equal(HttpStatusCode.OK, responseCourt.StatusCode);
        var resCourt = await responseCourt.Content.ReadFromJsonAsync<AuthResultDto>(_jsonOptions);
        Assert.Equal("Court", resCourt!.Role);
        Assert.Equal("/court/dashboard", resCourt.RedirectUrl);
        Assert.NotEmpty(resCourt.Token);

        // 2. Bank User Login
        var responseBank = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = "bank.user",
            Password = "Password@123"
        });
        Assert.Equal(HttpStatusCode.OK, responseBank.StatusCode);
        var resBank = await responseBank.Content.ReadFromJsonAsync<AuthResultDto>(_jsonOptions);
        Assert.Equal("Bank", resBank!.Role);
        Assert.Equal("/bank/dashboard", resBank.RedirectUrl);
        Assert.NotEmpty(resBank.Token);
    }

    [Theory]
    [InlineData("court.user", "WrongPassword@123")]
    [InlineData("non.existent.user", "Password@123")]
    [InlineData("", "Password@123")]
    [InlineData("court.user", "")]
    public async Task Test_Login_InvalidCredentials_ReturnsUnauthorizedOrBadRequest(string username, string password)
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = username,
            Password = password
        });

        // If username/password is empty, standard model validations might kick in returning 400 Bad Request, otherwise 401 Unauthorized.
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Test_ExpiredToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cases");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetExpiredToken());

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Test_UnauthorizedAccess_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/cases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Court Case Creation & Retrieval ---

    [Fact]
    public async Task Test_Court_CreateCase_Success_And_Retrieval()
    {
        await _factory.ResetDatabaseAsync();
        var token = await LoginAndGetTokenAsync("court.user", "Password@123");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create standard case
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("State Tax Dept"), "ComplainantName");
        content.Add(new StringContent("ABCDE1234F"), "ComplainantId");
        content.Add(new StringContent("Rajesh Kumar"), "DefendantName");
        content.Add(new StringContent("123456789012"), "DefendantId");
        content.Add(new StringContent("111122223333"), "DefendantAccountNumber");
        content.Add(new StringContent("SBI"), "DefendantBankName");
        content.Add(new StringContent("FreezeAccount"), "OrderType");
        content.Add(new StringContent("10000"), "FreezeAmount");

        content.Add(new ByteArrayContent(new byte[100]), "courtOrderFile", "order.pdf");
        content.Add(new ByteArrayContent(new byte[100]), "aadhaarFile", "aadhaar.jpg");
        content.Add(new ByteArrayContent(new byte[100]), "panFile", "pan.png");

        var response = await client.PostAsync("/api/cases", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var caseNumber = doc.RootElement.GetProperty("caseNumber").GetString();
        Assert.NotNull(caseNumber);
        Assert.StartsWith("CCMS-", caseNumber);

        // Fetch cases list (should contain created case)
        var listResponse = await client.GetAsync("/api/cases");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var cases = await listResponse.Content.ReadFromJsonAsync<List<CaseSummaryDto>>(_jsonOptions);
        Assert.Contains(cases, c => c.CaseNumber == caseNumber);
    }

    [Fact]
    public async Task Test_Court_CreateCase_ValidationFailures()
    {
        await _factory.ResetDatabaseAsync();
        var token = await LoginAndGetTokenAsync("court.user", "Password@123");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Scenario 1: Missing Complainant Name
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent("ABCDE1234F"), "ComplainantId");
            content.Add(new StringContent("Rajesh Kumar"), "DefendantName");
            content.Add(new StringContent("123456789012"), "DefendantId");
            content.Add(new StringContent("111122223333"), "DefendantAccountNumber");
            content.Add(new StringContent("SBI"), "DefendantBankName");
            content.Add(new StringContent("FreezeAccount"), "OrderType");
            content.Add(new ByteArrayContent(new byte[100]), "courtOrderFile", "order.pdf");
            content.Add(new ByteArrayContent(new byte[100]), "aadhaarFile", "aadhaar.jpg");
            content.Add(new ByteArrayContent(new byte[100]), "panFile", "pan.png");

            var response = await client.PostAsync("/api/cases", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Scenario 2: Unsupported executable file format
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent("State Tax Dept"), "ComplainantName");
            content.Add(new StringContent("ABCDE1234F"), "ComplainantId");
            content.Add(new StringContent("Rajesh Kumar"), "DefendantName");
            content.Add(new StringContent("123456789012"), "DefendantId");
            content.Add(new StringContent("111122223333"), "DefendantAccountNumber");
            content.Add(new StringContent("SBI"), "DefendantBankName");
            content.Add(new StringContent("FreezeAccount"), "OrderType");
            content.Add(new ByteArrayContent(new byte[100]), "courtOrderFile", "malware.exe"); // Executable
            content.Add(new ByteArrayContent(new byte[100]), "aadhaarFile", "aadhaar.jpg");
            content.Add(new ByteArrayContent(new byte[100]), "panFile", "pan.png");

            var response = await client.PostAsync("/api/cases", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Scenario 3: File size > 5 MB
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent("State Tax Dept"), "ComplainantName");
            content.Add(new StringContent("ABCDE1234F"), "ComplainantId");
            content.Add(new StringContent("Rajesh Kumar"), "DefendantName");
            content.Add(new StringContent("123456789012"), "DefendantId");
            content.Add(new StringContent("111122223333"), "DefendantAccountNumber");
            content.Add(new StringContent("SBI"), "DefendantBankName");
            content.Add(new StringContent("FreezeAccount"), "OrderType");
            content.Add(new ByteArrayContent(new byte[6 * 1024 * 1024]), "courtOrderFile", "order.pdf"); // 6MB file
            content.Add(new ByteArrayContent(new byte[100]), "aadhaarFile", "aadhaar.jpg");
            content.Add(new ByteArrayContent(new byte[100]), "panFile", "pan.png");

            var response = await client.PostAsync("/api/cases", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Scenario 4: Path traversal in file name
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent("State Tax Dept"), "ComplainantName");
            content.Add(new StringContent("ABCDE1234F"), "ComplainantId");
            content.Add(new StringContent("Rajesh Kumar"), "DefendantName");
            content.Add(new StringContent("123456789012"), "DefendantId");
            content.Add(new StringContent("111122223333"), "DefendantAccountNumber");
            content.Add(new StringContent("SBI"), "DefendantBankName");
            content.Add(new StringContent("FreezeAccount"), "OrderType");
            content.Add(new ByteArrayContent(new byte[100]), "courtOrderFile", "../../traversal.pdf");
            content.Add(new ByteArrayContent(new byte[100]), "aadhaarFile", "aadhaar.jpg");
            content.Add(new ByteArrayContent(new byte[100]), "panFile", "pan.png");

            var response = await client.PostAsync("/api/cases", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    // --- Batch Validation Job Workflow ---

    [Fact]
    public async Task Test_BatchValidation_EngineWorkflow()
    {
        await _factory.ResetDatabaseAsync();
        var bankToken = await LoginAndGetTokenAsync("bank.user", "Password@123");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bankToken);

        // Run batch manually
        var runResponse = await client.PostAsync("/api/batch/run", null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        // Verify status updates in AppDbContext
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Case A (CCMS-TEST-0001): Match by Account Number (Rajesh Kumar - SBI) -> AccountValidated
        var caseA = await context.Cases
            .Include(c => c.ValidationResult)
            .FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0001");
        Assert.NotNull(caseA);
        Assert.Equal(CaseStatus.AccountValidated, caseA.Status);
        Assert.NotNull(caseA.ValidationResult);
        Assert.Equal("111122223333", caseA.ValidationResult.MatchedAccountNumber);
        Assert.Equal("Rajesh Kumar", caseA.ValidationResult.AccountHolderName);
        Assert.Equal("Active", caseA.ValidationResult.AccountStatus);
        Assert.Equal(150000m, caseA.ValidationResult.CurrentBalance);

        // Case B (CCMS-TEST-0002): Match by Aadhaar (Priya Sharma - HDFC) -> AccountValidated
        var caseB = await context.Cases
            .Include(c => c.ValidationResult)
            .FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0002");
        Assert.NotNull(caseB);
        Assert.Equal(CaseStatus.AccountValidated, caseB.Status);
        Assert.NotNull(caseB.ValidationResult);
        Assert.Equal("444455556666", caseB.ValidationResult.MatchedAccountNumber);

        // Case C (CCMS-TEST-0003): Match by PAN (Amit Verma - SBI) -> AccountValidated
        var caseC = await context.Cases
            .Include(c => c.ValidationResult)
            .FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0003");
        Assert.NotNull(caseC);
        Assert.Equal(CaseStatus.AccountValidated, caseC.Status);
        Assert.NotNull(caseC.ValidationResult);
        Assert.Equal("777788889999", caseC.ValidationResult.MatchedAccountNumber);

        // Case D (CCMS-TEST-0004): No match (Unknown Person) -> AccountNotFound
        var caseD = await context.Cases
            .Include(c => c.ValidationResult)
            .Include(c => c.Response)
            .FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0004");
        Assert.NotNull(caseD);
        Assert.Equal(CaseStatus.AccountNotFound, caseD.Status);
        Assert.NotNull(caseD.Response); // Autoclosed / System responded
        Assert.Equal(ResponseType.AccountNotFound, caseD.Response.ResponseType);

        // Case E-H: Ensure terminal state cases weren't modified by batch
        var caseF = await context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0006");
        Assert.NotNull(caseF);
        Assert.Equal(CaseStatus.FreezeApplied, caseF.Status);

        var caseG = await context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0007");
        Assert.NotNull(caseG);
        Assert.Equal(CaseStatus.BalanceProvided, caseG.Status);
    }

    // --- Bank Action APIs (Inbox, Responses) ---

    [Fact]
    public async Task Test_Bank_Inbox_And_Responses()
    {
        await _factory.ResetDatabaseAsync();
        
        // 1. Check Inbox filtering for SBI Bank User
        var sbiToken = await LoginAndGetTokenAsync("bank.user", "Password@123");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sbiToken);

        var inboxResponse = await client.GetAsync("/api/cases/inbox");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);
        var inboxSBI = await inboxResponse.Content.ReadFromJsonAsync<CaseInboxDto>(_jsonOptions);
        Assert.NotNull(inboxSBI);

        // SBI inbox should contain Case E (CCMS-TEST-0005) in Awaiting Action (since Rajesh Kumar's bank is SBI)
        Assert.Contains(inboxSBI.AwaitingAction, c => c.CaseNumber == "CCMS-TEST-0005");
        // HDFC's Case G should NOT be in SBI inbox
        Assert.DoesNotContain(inboxSBI.Completed, c => c.CaseNumber == "CCMS-TEST-0007");

        // 2. Submit Freeze response to Case E (which is in AccountValidated)
        var submitResp = await client.PostAsJsonAsync("/api/cases/CCMS-TEST-0005/response", new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            FreezeAmountApplied = 12000m,
            Remarks = "Freeze applied successfully"
        });
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Verify case transition in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseE = await context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0005");
            Assert.Equal(CaseStatus.FreezeApplied, caseE!.Status);
        }

        // 3. Duplicate Prevention: Attempt to submit response to the same case again (should return 400/409)
        var secondSubmit = await client.PostAsJsonAsync("/api/cases/CCMS-TEST-0005/response", new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            FreezeAmountApplied = 12000m,
            Remarks = "Attempt duplicate freeze"
        });
        Assert.True(secondSubmit.StatusCode == HttpStatusCode.BadRequest || secondSubmit.StatusCode == HttpStatusCode.Conflict);

        // 4. Submit Balance response for another AccountValidated case.
        // Let's create an AccountValidated case for HDFC
        var hdfcToken = await LoginAndGetTokenAsync("hdfc.user", "Password@123");
        var clientHdfc = _factory.CreateClient();
        clientHdfc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hdfcToken);

        // First, check HDFC inbox
        var inboxHdfcResponse = await clientHdfc.GetAsync("/api/cases/inbox");
        var inboxHdfc = await inboxHdfcResponse.Content.ReadFromJsonAsync<CaseInboxDto>(_jsonOptions);
        Assert.Contains(inboxHdfc!.Completed, c => c.CaseNumber == "CCMS-TEST-0007"); // Case G belongs to HDFC

        // Let's manually transition Case B to AccountValidated (batch validation does this, let's trigger batch manual run)
        var runResponse = await clientHdfc.PostAsync("/api/batch/run", null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        // Case B (CCMS-TEST-0002) is HDFC and is now AccountValidated. Let's submit balance response
        var submitBalance = await clientHdfc.PostAsJsonAsync("/api/cases/CCMS-TEST-0002/response", new SubmitResponseDto
        {
            ResponseType = "BalanceProvided",
            BalanceReported = 75000m,
            Remarks = "Balance confirmed"
        });
        Assert.Equal(HttpStatusCode.OK, submitBalance.StatusCode);

        // Verify Case B is now BalanceProvided
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseB = await context.Cases.FirstOrDefaultAsync(c => c.CaseNumber == "CCMS-TEST-0002");
            Assert.Equal(CaseStatus.BalanceProvided, caseB!.Status);
        }
    }

    // --- Security Boundaries: Cross-Role Checks ---

    [Fact]
    public async Task Test_SecurityBoundaries_CrossRole_AccessDenied()
    {
        await _factory.ResetDatabaseAsync();
        
        var courtToken = await LoginAndGetTokenAsync("court.user", "Password@123");
        var bankToken = await LoginAndGetTokenAsync("bank.user", "Password@123");

        var client = _factory.CreateClient();

        // 1. Court user tries to access Bank APIs (e.g. GET inbox or POST response) -> Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", courtToken);
        var responseInbox = await client.GetAsync("/api/cases/inbox");
        Assert.Equal(HttpStatusCode.Forbidden, responseInbox.StatusCode);

        var responseSubmit = await client.PostAsJsonAsync("/api/cases/CCMS-TEST-0005/response", new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            Remarks = "Attacker remark"
        });
        Assert.Equal(HttpStatusCode.Forbidden, responseSubmit.StatusCode);

        // 2. Bank user tries to call Court APIs (e.g. Create Case) -> Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bankToken);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("State Tax Dept"), "ComplainantName");
        content.Add(new StringContent("ABCDE1234F"), "ComplainantId");
        content.Add(new StringContent("Rajesh Kumar"), "DefendantName");
        content.Add(new StringContent("123456789012"), "DefendantId");
        content.Add(new StringContent("111122223333"), "DefendantAccountNumber");
        content.Add(new StringContent("SBI"), "DefendantBankName");
        content.Add(new StringContent("FreezeAccount"), "OrderType");
        content.Add(new StringContent("10000"), "FreezeAmount");
        content.Add(new ByteArrayContent(new byte[100]), "courtOrderFile", "order.pdf");
        content.Add(new ByteArrayContent(new byte[100]), "aadhaarFile", "aadhaar.jpg");
        content.Add(new ByteArrayContent(new byte[100]), "panFile", "pan.png");

        var responseCreate = await client.PostAsync("/api/cases", content);
        Assert.Equal(HttpStatusCode.Forbidden, responseCreate.StatusCode);
    }
}
