using Xunit;
using FluentAssertions;
using CCMS.Application.Services;
using CCMS.Application.Interfaces;
using CCMS.Infrastructure.Services;

namespace CCMS.Application.Tests.UnitTests;

public class DataMaskingHelperTests
{
    [Theory]
    [InlineData("123456789012", "********9012")]
    [InlineData("1234-5678-9012", "****-****-9012")]
    [InlineData("Aadhaar: 1234-5678-9012", "Aadhaar: ****-****-9012")]
    [InlineData("aadhaar number", "**********mber")]
    public void MaskIdentity_ShouldMaskAadhaarNumbers_Correctly(string input, string expected)
    {
        // Act
        var result = DataMaskingHelper.MaskIdentity(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCDE1234F", "******234F")]
    [InlineData("XYZ12345", "****2345")]
    [InlineData("12345", "*2345")]
    public void MaskIdentity_ShouldMaskOtherIdentityTypes_LeavingLastFourVisible(string input, string expected)
    {
        // Act
        var result = DataMaskingHelper.MaskIdentity(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123456789012", "********9012")]
    [InlineData("9876543210", "******3210")]
    [InlineData("50200004558291", "**********8291")]
    [InlineData("123", "***")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void MaskAccount_ShouldMaskAccountNumbers_LeavingLastFourDigits(string? input, string expected)
    {
        // Act
        var result = DataMaskingHelper.MaskAccount(input);

        // Assert
        result.Should().Be(expected);
    }
}
