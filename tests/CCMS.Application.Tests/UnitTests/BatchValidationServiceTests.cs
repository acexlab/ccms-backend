using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FluentAssertions;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;
using CCMS.Application.Services;
using CCMS.Application.Interfaces;
using CCMS.Infrastructure.Services;

namespace CCMS.Application.Tests.UnitTests;

public class BatchValidationServiceTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private Mock<IBatchJobLogRepository> GetMockBatchLogRepository()
    {
        var mock = new Mock<IBatchJobLogRepository>();
        mock.Setup(r => r.AddLogAsync(It.IsAny<BatchJobLog>()))
            .ReturnsAsync((BatchJobLog log) => log);
        return mock;
    }

    private Mock<ICaseRepository> GetMockCaseRepository()
    {
        return new Mock<ICaseRepository>();
    }

    [Fact]
    public async Task TriggerBatchRunAsync_ShouldMatchByAccountNumber_First()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogRepo = GetMockBatchLogRepository();
        var mockCaseRepo = GetMockCaseRepository();
        var service = new BatchValidationService(mockLogRepo.Object, mockCaseRepo.Object, context);

        var pendingCase = new Case
        {
            CaseNumber = "CCMS-20260616-0001",
            OrderType = OrderType.FreezeAccount,
            Status = CaseStatus.Pending,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "9876543210",
                IdentityNumber = "Aadhaar: 1234-5678-9012"
            }
        };

        var bankCustomer = new BankCustomer
        {
            AccountNumber = "9876543210",
            AadhaarNumber = "1234-5678-9012",
            PANNumber = "ABCDE1234F",
            AccountHolderName = "John Smith",
            AccountStatus = AccountStatus.Active,
            CurrentBalance = 50000.00m
        };

        context.Cases.Add(pendingCase);
        context.BankCustomers.Add(bankCustomer);
        await context.SaveChangesAsync();

        // Act
        var log = await service.TriggerBatchRunAsync(TriggeredBy.Manual, 1);

        // Assert
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.AccountValidated);
        
        var validationResult = await context.CaseValidationResults.FirstOrDefaultAsync(r => r.CaseId == updatedCase.Id);
        validationResult.Should().NotBeNull();
        validationResult!.MatchedOn.Should().Be(MatchedOn.AccountNumber);
        validationResult.MatchedAccountNumber.Should().Be("9876543210");
        log.AccountsMatched.Should().Be(1);
    }

    [Fact]
    public async Task TriggerBatchRunAsync_ShouldFallbackToAadhaar_IfAccountNumberNotProvided()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogRepo = GetMockBatchLogRepository();
        var mockCaseRepo = GetMockCaseRepository();
        var service = new BatchValidationService(mockLogRepo.Object, mockCaseRepo.Object, context);

        var pendingCase = new Case
        {
            CaseNumber = "CCMS-20260616-0002",
            OrderType = OrderType.FreezeAccount,
            Status = CaseStatus.Pending,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "", // Empty account number
                IdentityNumber = "Aadhaar: 1234-5678-9012"
            }
        };

        var bankCustomer = new BankCustomer
        {
            AccountNumber = "9876543210",
            AadhaarNumber = "1234-5678-9012",
            PANNumber = "ABCDE1234F",
            AccountHolderName = "John Smith",
            AccountStatus = AccountStatus.Active,
            CurrentBalance = 50000.00m
        };

        context.Cases.Add(pendingCase);
        context.BankCustomers.Add(bankCustomer);
        await context.SaveChangesAsync();

        // Act
        var log = await service.TriggerBatchRunAsync(TriggeredBy.Manual, 1);

        // Assert
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.AccountValidated);
        
        var validationResult = await context.CaseValidationResults.FirstOrDefaultAsync(r => r.CaseId == updatedCase.Id);
        validationResult.Should().NotBeNull();
        validationResult!.MatchedOn.Should().Be(MatchedOn.Aadhaar);
        log.AccountsMatched.Should().Be(1);
    }

    [Fact]
    public async Task TriggerBatchRunAsync_ShouldFallbackToPAN_IfAadhaarFailsOrNotProvided()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogRepo = GetMockBatchLogRepository();
        var mockCaseRepo = GetMockCaseRepository();
        var service = new BatchValidationService(mockLogRepo.Object, mockCaseRepo.Object, context);

        var pendingCase = new Case
        {
            CaseNumber = "CCMS-20260616-0003",
            OrderType = OrderType.FreezeAccount,
            Status = CaseStatus.Pending,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "",
                IdentityNumber = "PAN: ABCDE1234F" // Only PAN is provided
            }
        };

        var bankCustomer = new BankCustomer
        {
            AccountNumber = "9876543210",
            AadhaarNumber = "1234-5678-9012",
            PANNumber = "ABCDE1234F",
            AccountHolderName = "John Smith",
            AccountStatus = AccountStatus.Active,
            CurrentBalance = 50000.00m
        };

        context.Cases.Add(pendingCase);
        context.BankCustomers.Add(bankCustomer);
        await context.SaveChangesAsync();

        // Act
        var log = await service.TriggerBatchRunAsync(TriggeredBy.Manual, 1);

        // Assert
        var updatedCase = await context.Cases.FirstAsync();
        updatedCase.Status.Should().Be(CaseStatus.AccountValidated);
        
        var validationResult = await context.CaseValidationResults.FirstOrDefaultAsync(r => r.CaseId == updatedCase.Id);
        validationResult.Should().NotBeNull();
        validationResult!.MatchedOn.Should().Be(MatchedOn.PAN);
    }

    [Fact]
    public async Task TriggerBatchRunAsync_ShouldOnlyProcessPendingCases_AndSkipValidatedOrClosed()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogRepo = GetMockBatchLogRepository();
        var mockCaseRepo = GetMockCaseRepository();
        var service = new BatchValidationService(mockLogRepo.Object, mockCaseRepo.Object, context);

        var validatedCase = new Case
        {
            CaseNumber = "CCMS-20260616-0004",
            OrderType = OrderType.FreezeAccount,
            Status = CaseStatus.AccountValidated,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "9876543210"
            }
        };

        var closedCase = new Case
        {
            CaseNumber = "CCMS-20260616-0005",
            OrderType = OrderType.FreezeAccount,
            Status = CaseStatus.FreezeApplied,
            CreatedByUserId = 1,
            Defendant = new Defendant
            {
                FullName = "John Smith",
                BankAccountNumber = "9876543210"
            }
        };

        context.Cases.AddRange(validatedCase, closedCase);
        await context.SaveChangesAsync();

        // Act
        var log = await service.TriggerBatchRunAsync(TriggeredBy.Manual, 1);

        // Assert
        log.CasesProcessed.Should().Be(0); // No Pending cases were processed
        var cases = await context.Cases.ToListAsync();
        cases.First(c => c.CaseNumber == "CCMS-20260616-0004").Status.Should().Be(CaseStatus.AccountValidated);
        cases.First(c => c.CaseNumber == "CCMS-20260616-0005").Status.Should().Be(CaseStatus.FreezeApplied);
    }
}
