using System.Text.RegularExpressions;

namespace CCMS.Application.Services;

public static class DataMaskingHelper
{
    public static string MaskIdentity(string? identity)
    {
        if (string.IsNullOrEmpty(identity)) return string.Empty;
        
        var masked = identity;
        
        // Mask Aadhaar (keep last 4 digits)
        masked = Regex.Replace(masked, @"\b(\d{4}[-\s]?\d{4}[-\s]?)(\d{4})\b", match => 
        {
            var prefix = match.Groups[1].Value;
            var last4 = match.Groups[2].Value;
            return Regex.Replace(prefix, @"\d", "*") + last4;
        });

        // Mask PAN (keep last 4 chars)
        masked = Regex.Replace(masked, @"\b([A-Za-z]{5}\d{4}[A-Za-z])\b", match => 
        {
            var val = match.Value;
            return new string('*', val.Length - 4) + val.Substring(val.Length - 4);
        });

        // If no format matched and it's a simple string, mask all but last 4
        if (masked == identity) 
        {
            if (identity.Length > 4) 
                return new string('*', identity.Length - 4) + identity.Substring(identity.Length - 4);
        }

        return masked;
    }

    public static string MaskAccount(string? account)
    {
        if (string.IsNullOrEmpty(account)) return string.Empty;
        if (account.Length <= 4) return new string('*', account.Length);
        return new string('*', account.Length - 4) + account.Substring(account.Length - 4);
    }
}
