using System.Threading.Channels;
using DogPhoto.Infrastructure.Persistence.Portfolio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DogPhoto.Infrastructure.ImagePipeline;

public interface IImageProcessingQueue
{
    ValueTask EnqueueAsync(Guid photoId, CancellationToken cancellationToken = default);
}

public class ImageProcessingQueue : IImageProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid photoId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(photoId, cancellationToken);

    public ChannelReader<Guid> Reader => _channel.Reader;
}

public class ImageProcessingWorker : BackgroundService
{
    private readonly ImageProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageProcessingWorker> _logger;

    public ImageProcessingWorker(
        ImageProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ImageProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Image processing worker started");

        await foreach (var photoId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessPhotoAsync(photoId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process photo {PhotoId}", photoId);
            }
        }
    }

    private async Task ProcessPhotoAsync(Guid photoId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing photo {PhotoId}", photoId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<BlobStorage.IBlobStorageService>();
        var imageProcessor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();

        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);
        if (photo is null)
        {
            _logger.LogWarning("Photo {PhotoId} not found", photoId);
            return;
        }

        if (string.IsNullOrEmpty(photo.OriginalBlobUrl))
        {
            _logger.LogWarning("Photo {PhotoId} has no original blob URL", photoId);
            return;
        }

        // Download original from blob storage
        // Extract container and blob name from the stored URL
        var uri = new Uri(photo.OriginalBlobUrl);
        var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/', 3);
        // pathSegments: [accountName, containerName, blobName] or [containerName, blobName]
        var containerName = pathSegments.Length >= 3 ? pathSegments[1] : pathSegments[0];
        var blobName = pathSegments.Length >= 3 ? pathSegments[2] : pathSegments[1];
        using var downloadStream = await blobStorage.DownloadAsync(containerName, blobName);

        // Process image
        var result = await imageProcessor.ProcessAsync(photoId, downloadStream);

        // Update photo metadata
        photo.Width = result.Metadata.Width;
        photo.Height = result.Metadata.Height;
        photo.CameraSettings = result.Metadata.CameraSettings;
        photo.DominantColor = result.Metadata.DominantColor;
        photo.Blurhash = result.Metadata.Blurhash;
        if (result.Metadata.ShotDate.HasValue)
            photo.ShotDate = result.Metadata.ShotDate;
        photo.UpdatedAt = DateTime.UtcNow;

        // Save variants
        foreach (var variant in result.Variants)
        {
            db.PhotoVariants.Add(new PhotoVariant
            {
                PhotoId = photoId,
                Width = variant.Width,
                Height = variant.Height,
                Format = variant.Format,
                Quality = variant.Quality,
                BlobUrl = variant.BlobUrl,
                SizeBytes = variant.SizeBytes
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Photo {PhotoId} processed: {VariantCount} variants generated", photoId, result.Variants.Count);
    }
}
