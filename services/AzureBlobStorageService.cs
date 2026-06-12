/*
 * File: AzureBlobStorageService.cs
 * Description: Stores uploaded files in Azure Blob Storage container (intended for production environment).
 * To Implement: Wire connection strings via Kubernetes secrets at runtime.
 */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace ccms_backend.services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:AzureBlobConnection"] 
            ?? configuration["FileStorage:AzureBlobConnectionString"]
            ?? throw new ArgumentNullException("Azure Blob connection string is not configured.");
        
        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerName = configuration["FileStorage:AzureBlobContainerName"] ?? "ccms-documents";
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string containerPath, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobPath = $"{containerPath}/{Guid.NewGuid()}_{fileName}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken: ct);

        // Return the full URI string or path for reference
        return blobClient.Uri.ToString();
    }

    public async Task DeleteFileAsync(string filePath, CancellationToken ct = default)
    {
        var uri = new Uri(filePath);
        var blobClient = new BlobClient(uri);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public string GetFileUrl(string filePath)
    {
        // For Azure blobs, the filePath stored is already the public/authenticated URL or Uri
        return filePath;
    }
}
