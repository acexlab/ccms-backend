using System.IO;
using System.Threading.Tasks;

namespace ccms_backend.services;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string blobPath, string contentType);
    string GenerateSasUri(string blobName, int expiryMinutes = 15);
    Task DeleteFileAsync(string blobName);
}