/*
 * File: LocalFileStorageService.cs
 * Description: Stores uploaded files locally on the disk (intended for dev/staging environments).
 * To Implement: Align storage modes with docker volume mapping settings.
 */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ccms_backend.services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = configuration["FileStorage:LocalBasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        _baseUrl = configuration["FileStorage:BaseUrl"] ?? "http://localhost:5000/uploads";
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string containerPath, CancellationToken ct = default)
    {
        var targetFolder = Path.Combine(_basePath, containerPath);
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var fullPath = Path.Combine(targetFolder, uniqueFileName);

        using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await fileStream.CopyToAsync(destination, ct);

        // Return the relative path of the file
        return Path.Combine(containerPath, uniqueFileName).Replace('\\', '/');
    }

    public Task DeleteFileAsync(string filePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public string GetFileUrl(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return string.Empty;
        return $"{_baseUrl}/{filePath.Replace('\\', '/')}";
    }
}
