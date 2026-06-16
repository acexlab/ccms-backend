using System;

namespace ccms_backend.services;

public static class DataMaskingHelper
{
    public static string MaskAadhaar(string? aadhaar)
    {
        if (string.IsNullOrEmpty(aadhaar) || aadhaar.Length < 4) return aadhaar ?? string.Empty;
        return "XXXX XXXX " + aadhaar.Substring(aadhaar.Length - 4);
    }

    public static string MaskPan(string? pan)
    {
        if (string.IsNullOrEmpty(pan) || pan.Length < 4) return pan ?? string.Empty;
        return "XXXXXXXXX" + pan.Substring(pan.Length - 4);
    }

    public static string MaskAccount(string? account)
    {
        if (string.IsNullOrEmpty(account) || account.Length < 4) return account ?? string.Empty;
        return "XXXXXXXXXXXX" + account.Substring(account.Length - 4);
    }
}