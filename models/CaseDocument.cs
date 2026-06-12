/*
 * File: CaseDocument.cs
 * Description: Represents an uploaded file attachment supporting a court case order.
 * To Implement: Mapping for document paths.
 */

using System;

namespace ccms_backend.models;

public class CaseDocument
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Case Case { get; set; } = null!;
}
