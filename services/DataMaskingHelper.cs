/*
 * File: DataMaskingHelper.cs
 * Description: Obfuscates sensitive fields before returning them to client callers.
 * To Implement: Masking is applied at the Application service retrieval layer.
 */

using System;

namespace ccms_backend.services;

public static class DataMaskingHelper
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 4) return new string('*', value.Length);
        return $"****{value[^4..]}";
    }

    public static string MaskAadhaar(string value) => Mask(value);
    public static string MaskPan(string value) => Mask(value);
    public static string MaskAccountNumber(string value) => Mask(value);
}
// Note: Matches masking pattern logic of frontend mask.pipe.ts.
