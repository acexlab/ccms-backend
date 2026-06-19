using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using CCMS.Application.DTOs;

namespace CCMS.API.Tests.IntegrationTests;

public class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousRequest_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient(); // No auth headers added

        // Act
        var response = await client.GetAsync("/api/cases/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CourtOfficer_ShouldBeForbidden_FromAccessingBankEndpoints()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "court_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Court"); // Has Court role, not Bank

        // Act - POST /api/batch/run is Bank only
        var response = await client.PostAsync("/api/batch/run", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BankOfficer_ShouldBeForbidden_FromAccessingCourtEndpoints()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Username", "bank_officer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Bank"); // Has Bank role, not Court

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("State Legal Authority"), "ComplainantName");

        // Act - POST /api/cases is Court only
        var response = await client.PostAsync("/api/cases", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SqlInjection_AttemptOnLogin_ShouldFailSafely()
    {
        // Arrange
        var client = _factory.CreateClient();
        var loginDto = new LoginDto 
        { 
            Username = "admin' OR '1'='1", 
            Password = "password" 
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        // Application should use parameterized queries and gracefully return 401 Unauthorized, not 500 error or 200 OK.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
