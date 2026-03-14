namespace DogPhoto.Infrastructure.BlobStorage;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType);
    Task<Stream> DownloadAsync(string containerName, string blobName);
    Task<string> GetBlobUrlAsync(string containerName, string blobName);
    Task EnsureContainersExistAsync();
}
