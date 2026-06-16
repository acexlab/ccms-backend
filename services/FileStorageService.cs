using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace ccms_backend.services;

public class FileStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public FileStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["AZURE_STORAGE_CONTAINER_NAME"] ?? "case-documents";
    }

    private BlobContainerClient GetContainerClient()
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        // Ensure the container exists
        containerClient.CreateIfNotExists(PublicAccessType.None);
        return containerClient;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string blobPath, string contentType)
    {
        var containerClient = GetContainerClient();
        var blobClient = containerClient.GetBlobClient(blobPath);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await blobClient.UploadAsync(fileStream, options);
        return blobPath;
    }

    public string GenerateSasUri(string blobName, int expiryMinutes = 15)
    {
        var containerClient = GetContainerClient();
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("BlobClient cannot generate SAS URIs. Ensure the storage connection has credentials that allow SAS generation.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri.ToString();
    }

    public async Task DeleteFileAsync(string blobName)
    {
        var containerClient = GetContainerClient();
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
    }
}
