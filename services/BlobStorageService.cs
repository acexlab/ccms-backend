using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ccms_backend.services;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream?> DownloadFileAsync(string blobName);
}

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = configuration["AZURE_STORAGE_CONNECTION_STRING"] ?? configuration["BlobStorage:ConnectionString"];
        _containerName = configuration["AZURE_STORAGE_CONTAINER_NAME"] ?? configuration["BlobStorage:ContainerName"] ?? "ccms-uploads";

        if (string.IsNullOrEmpty(connectionString) || connectionString == "UseDevelopmentStorage=true;")
        {
            _logger.LogWarning("Azure Blob Storage connection string is missing or set to local. Uploads will fail unless configured.");
            // We'll still initialize it so it doesn't crash on startup, but actual calls will fail if connection string is invalid.
            try
            {
                _blobServiceClient = new BlobServiceClient(connectionString ?? "UseDevelopmentStorage=true;");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize BlobServiceClient.");
                _blobServiceClient = null!;
            }
        }
        else
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
    }

    private async Task EnsureContainerExistsAsync()
    {
        if (_blobServiceClient == null) return;
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        if (_blobServiceClient == null)
            throw new InvalidOperationException("BlobServiceClient is not initialized.");

        await EnsureContainerExistsAsync();

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        fileStream.Position = 0; // Ensure stream is at beginning
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = blobHttpHeaders });

        return fileName; // We return the name to store in the DB
    }

    public async Task<Stream?> DownloadFileAsync(string blobName)
    {
        if (_blobServiceClient == null) return null;

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (await blobClient.ExistsAsync())
            {
                var response = await blobClient.DownloadAsync();
                return response.Value.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading blob {BlobName}", blobName);
        }

        return null;
    }
}
