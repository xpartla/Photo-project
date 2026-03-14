using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace DogPhoto.Infrastructure.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
    public const string OriginalsContainer = "originals";
    public const string ProcessedContainer = "processed";

    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["Azure:BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Azure:BlobStorage:ConnectionString not configured.");
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blobClient.Uri.ToString();
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var ms = new MemoryStream();
        await blobClient.DownloadToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    public Task<string> GetBlobUrlAsync(string containerName, string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        return Task.FromResult(blobClient.Uri.ToString());
    }

    public async Task EnsureContainersExistAsync()
    {
        // originals — private access
        var originalsClient = _blobServiceClient.GetBlobContainerClient(OriginalsContainer);
        await originalsClient.CreateIfNotExistsAsync(PublicAccessType.None);

        // processed — public blob-level access for serving images
        var processedClient = _blobServiceClient.GetBlobContainerClient(ProcessedContainer);
        await processedClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
    }
}
