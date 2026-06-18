using CCMS.Application.DTOs;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;
using CCMS.Application.Services;
using CCMS.Application.Interfaces;
using CCMS.Infrastructure.Services;
using CCMS.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using Microsoft.Extensions.Logging;

namespace CCMS.API.Tests.UnitTests;

public class CaseStateTransitionTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private CasesController CreateControllerWithUser(AppDbContext context, string username, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("unique_name", username),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var controllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        var mockBlobStorage = new Mock<IBlobStorageService>();
        var caseService = new CaseService(context, mockBlobStorage.Object);
        var mockLogger = new Mock<ILogger<CasesController>>();
        return new CasesController(caseService, mockLogger.Object)
        {
            ControllerContext = controllerContext
        };
    }

    [Fact]
    public async Task StateTransition_PendingToAccountValidated_WhenBatchFindsAccount()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogRepo = new Mock<IBatchJobLogRepository>();
        mockLogRepo.Setup(r => r.AddLogAsync(It.IsAny<BatchJobLog>())).ReturnsAsync((BatchJobLog l) => l);
        var service = new BatchValidationService(mockLogRepo.Object, new Mock<ICaseRepository>().Object, context);

        var @case = new Case
        {
            CaseNumber = "CCMS-20260616-0001",
            Status = CaseStatus.Pending,
            OrderType = OrderType.FreezeAccount,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "9876543210"
            }
        };

        var customer = new BankCustomer
        {
            AccountNumber = "9876543210",
            AccountHolderName = "John Smith",
            AccountStatus = AccountStatus.Active,
            CurrentBalance = 100.00m
        };

        context.Cases.Add(@case);
        context.BankCustomers.Add(customer);
        await context.SaveChangesAsync();

        // Act
        await service.TriggerBatchRunAsync(TriggeredBy.Manual, 1);

        // Assert
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.AccountValidated);
    }

    [Fact]
    public async Task StateTransition_PendingToAccountNotFound_WhenBatchFindsNoAccount()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogRepo = new Mock<IBatchJobLogRepository>();
        mockLogRepo.Setup(r => r.AddLogAsync(It.IsAny<BatchJobLog>())).ReturnsAsync((BatchJobLog l) => l);
        var service = new BatchValidationService(mockLogRepo.Object, new Mock<ICaseRepository>().Object, context);

        var @case = new Case
        {
            CaseNumber = "CCMS-20260616-0002",
            Status = CaseStatus.Pending,
            OrderType = OrderType.FreezeAccount,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "9876543210"
            }
        };

        // No BankCustomer seeded
        context.Cases.Add(@case);
        await context.SaveChangesAsync();

        // Act
        await service.TriggerBatchRunAsync(TriggeredBy.Manual, 1);

        // Assert
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.AccountNotFound); // Terminal auto-resolved state
        
        var response = await context.CaseResponses.FirstOrDefaultAsync(r => r.CaseId == updatedCase.Id);
        response.Should().NotBeNull();
        response!.ResponseType.Should().Be(ResponseType.AccountNotFound);
    }

    [Fact]
    public async Task StateTransition_AccountValidatedToFreezeApplied_WhenBankOfficerSubmitsFreeze()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var @case = new Case
        {
            Id = 1,
            CaseNumber = "CCMS-20260616-0003",
            Status = CaseStatus.AccountValidated,
            OrderType = OrderType.FreezeAccount,
            CreatedByUserId = 1
        };
        var officer = new User { Id = 2, Username = "bank_officer", Role = UserRole.Bank, PasswordHash = "" };
        context.Cases.Add(@case);
        context.Users.Add(officer);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithUser(context, "bank_officer", "Bank");
        var dto = new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            FreezeAmountApplied = 15000.00m,
            Remarks = "Freeze order executed successfully"
        };

        // Act
        var result = await controller.SubmitResponse("CCMS-20260616-0003", dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.FreezeApplied);
    }

    [Fact]
    public async Task StateTransition_AccountValidatedToBalanceProvided_WhenBankOfficerSubmitsBalance()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var @case = new Case
        {
            Id = 1,
            CaseNumber = "CCMS-20260616-0004",
            Status = CaseStatus.AccountValidated,
            OrderType = OrderType.BalanceEnquiry,
            CreatedByUserId = 1
        };
        var officer = new User { Id = 2, Username = "bank_officer", Role = UserRole.Bank, PasswordHash = "" };
        context.Cases.Add(@case);
        context.Users.Add(officer);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithUser(context, "bank_officer", "Bank");
        var dto = new SubmitResponseDto
        {
            ResponseType = "BalanceProvided",
            BalanceReported = 25000.00m,
            Remarks = "Balance verification completed"
        };

        // Act
        var result = await controller.SubmitResponse("CCMS-20260616-0004", dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.BalanceProvided);
    }

    [Theory]
    [InlineData(CaseStatus.FreezeApplied)]
    [InlineData(CaseStatus.BalanceProvided)]
    [InlineData(CaseStatus.AccountNotFound)]
    public async Task StateTransition_ShouldThrowException_WhenCaseIsAlreadyInTerminalState(CaseStatus terminalStatus)
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var @case = new Case
        {
            Id = 1,
            CaseNumber = "CCMS-20260616-0005",
            Status = terminalStatus,
            OrderType = OrderType.FreezeAccount,
            CreatedByUserId = 1
        };
        var officer = new User { Id = 2, Username = "bank_officer", Role = UserRole.Bank, PasswordHash = "" };
        context.Cases.Add(@case);
        context.Users.Add(officer);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithUser(context, "bank_officer", "Bank");
        var dto = new SubmitResponseDto
        {
            ResponseType = "FreezeApplied",
            FreezeAmountApplied = 15000.00m,
            Remarks = "Another attempt"
        };

        // Act
        var result = await controller.SubmitResponse("CCMS-20260616-0005", dto);
        
        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
