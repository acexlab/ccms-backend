using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;

namespace ccms_backend.services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly LocalFileStorageService _fallbackStorage;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        _fallbackStorage = new LocalFileStorageService();
        var connectionString = configuration.GetConnectionString("AzureBlobStorage");
        var containerName = configuration.GetValue<string>("AzureBlobStorage:ContainerName", "ccms-attachments");

        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                var serviceClient = new BlobServiceClient(connectionString);
                _containerClient = serviceClient.GetBlobContainerClient(containerName);
                _containerClient.CreateIfNotExists(Azure.Storage.Blobs.Models.PublicAccessType.None);
            }
            catch
            {
                _containerClient = null;
            }
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
    {
        if (_containerClient == null)
        {
            return await _fallbackStorage.SaveFileAsync(fileStream, fileName);
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var blobClient = _containerClient.GetBlobClient(uniqueFileName);
        await blobClient.UploadAsync(fileStream, true);
        return uniqueFileName;
    }

    public async Task<Stream> GetFileAsync(string filePath)
    {
        if (_containerClient == null || filePath.StartsWith("/uploads/"))
        {
            return await _fallbackStorage.GetFileAsync(filePath);
        }

        var blobClient = _containerClient.GetBlobClient(filePath);
        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        if (_containerClient == null || filePath.StartsWith("/uploads/"))
        {
            await _fallbackStorage.DeleteFileAsync(filePath);
            return;
        }

        var blobClient = _containerClient.GetBlobClient(filePath);
        await blobClient.DeleteIfExistsAsync();
    }
}