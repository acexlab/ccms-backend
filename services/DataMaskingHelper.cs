using System;
using System.Text.RegularExpressions;

namespace ccms_backend.services;

public static class DataMaskingHelper
{
    public static string MaskIdentity(string? identityNumber)
    {
        if (string.IsNullOrWhiteSpace(identityNumber))
            return string.Empty;

        var clean = identityNumber.Replace("-", "").Replace(" ", "");

        // Aadhaar check: 12-digit numeric, or if it explicitly contains the word "Aadhaar"
        if (Regex.IsMatch(clean, @"^\d{12}$") || identityNumber.Contains("Aadhaar", StringComparison.OrdinalIgnoreCase))
        {
            return "[Aadhaar Redacted]";
        }

        // PAN or other formats: mask all except the last 4 characters
        return MaskTrailing(identityNumber, 4);
    }

    public static string MaskAccount(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return string.Empty;

        return MaskTrailing(accountNumber, 4);
    }

    private static string MaskTrailing(string value, int keepCount)
    {
        if (value.Length <= keepCount)
        {
            return new string('*', value.Length);
        }

        var maskedPart = new string('*', value.Length - keepCount);
        var visiblePart = value.Substring(value.Length - keepCount);
        return maskedPart + visiblePart;
    }
}