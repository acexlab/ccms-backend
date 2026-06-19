using CCMS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CCMS.Infrastructure.Services;

/// <summary>
/// Local file system implementation of IBlobStorageService.
/// Used in development when Azure Blob Storage is not configured.
/// Files are saved to wwwroot/uploads/ inside the API project.
/// </summary>
public class LocalFileStorageService : IBlobStorageService
{
    private readonly string _uploadPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        // Save files to wwwroot/uploads relative to the running app
        var basePath = AppContext.BaseDirectory;
        _uploadPath = Path.Combine(basePath, "wwwroot", "uploads");
        Directory.CreateDirectory(_uploadPath); // Ensure folder exists
        _logger.LogInformation("LocalFileStorageService initialized. Upload path: {Path}", _uploadPath);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var filePath = Path.Combine(_uploadPath, fileName);
        fileStream.Position = 0;
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(fs);
        _logger.LogInformation("File saved locally: {FileName}", fileName);
        return fileName;
    }

    public async Task<Stream?> DownloadFileAsync(string blobName)
    {
        var filePath = Path.Combine(_uploadPath, blobName);
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Local file not found: {FilePath}", filePath);
            return null;
        }

        // Read into memory stream so the file handle is released
        var memStream = new MemoryStream();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await fs.CopyToAsync(memStream);
        memStream.Position = 0;
        return memStream;
    }
}
