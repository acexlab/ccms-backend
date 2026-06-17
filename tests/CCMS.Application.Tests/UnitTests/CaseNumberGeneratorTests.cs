using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using CCMS.Infrastructure.Data;
using CCMS.Domain.Entities;
using CCMS.Application.Services;
using CCMS.Application.Interfaces;
using CCMS.Infrastructure.Services;

namespace CCMS.Application.Tests.UnitTests;

public class CaseNumberGeneratorTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GenerateAsync_ShouldFormatCaseNumber_WithTodayDateAndSequence()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var generator = new CaseNumberGenerator(context);
        var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");

        // Act
        var result = await generator.GenerateAsync();

        // Assert
        result.Should().StartWith($"CCMS-{todayStr}-");
        result.Should().MatchRegex(@"^CCMS-\d{8}-\d{4}$");
        result.Should().EndWith("0001");
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncrementSequence_ForSubsequentCasesOnSameDay()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var generator = new CaseNumberGenerator(context);
        
        // Seed first case today
        context.Cases.Add(new Case
        {
            CaseNumber = "CCMS-20260616-0001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OrderType = OrderType.FreezeAccount,
            Status = CaseStatus.Pending,
            CreatedByUserId = 1
        });
        await context.SaveChangesAsync();

        // Act
        var result = await generator.GenerateAsync();

        // Assert
        result.Should().EndWith("0002");
    }
}
