using CCMS.Domain.Enums;
using System;

namespace CCMS.Domain.Entities;

public class CaseDocument
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
