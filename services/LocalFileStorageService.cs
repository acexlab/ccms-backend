using System;
using System.IO;
using System.Threading.Tasks;

namespace ccms_backend.services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageFolder;

    public LocalFileStorageService()
    {
        _storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(_storageFolder))
        {
            Directory.CreateDirectory(_storageFolder);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
    {
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var physicalPath = Path.Combine(_storageFolder, uniqueFileName);
        using (var file = new FileStream(physicalPath, FileMode.Create, FileAccess.Write))
        {
            await fileStream.CopyToAsync(file);
        }
        return $"/uploads/{uniqueFileName}";
    }

    public Task<Stream> GetFileAsync(string filePath)
    {
        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
        if (!File.Exists(physicalPath))
        {
            throw new FileNotFoundException("File not found on local storage", physicalPath);
        }
        return Task.FromResult<Stream>(File.OpenRead(physicalPath));
    }

    public Task DeleteFileAsync(string filePath)
    {
        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
        return Task.CompletedTask;
    }
}