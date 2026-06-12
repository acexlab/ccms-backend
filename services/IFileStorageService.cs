/*
 * File: IFileStorageService.cs
 * Description: Interface defining contract for saving, deleting, and getting URLs of case files.
 * To Implement: Implement local disk storage for dev and Azure Blob Storage for production.
 */

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ccms_backend.services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string containerPath, CancellationToken ct = default);
    Task DeleteFileAsync(string filePath, CancellationToken ct = default);
    string GetFileUrl(string filePath);
}
