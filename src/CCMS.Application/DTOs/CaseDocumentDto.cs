namespace CCMS.Application.DTOs;

public class CaseDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
