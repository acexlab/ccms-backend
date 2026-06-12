/*
 * File: CaseDocumentDto.cs
 * Description: Data transfer object representing metadata and download URL of a Case Document.
 * To Implement: Keep in sync with FileStorageService output URL formats.
 */

using System;

namespace ccms_backend.dtos;

public class CaseDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
