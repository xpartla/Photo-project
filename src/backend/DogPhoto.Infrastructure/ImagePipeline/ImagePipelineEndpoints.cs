using DogPhoto.Infrastructure.BlobStorage;
using DogPhoto.Infrastructure.Persistence.Portfolio;
using DogPhoto.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DogPhoto.Infrastructure.ImagePipeline;

public static class ImagePipelineEndpoints
{
    public static void MapImagePipelineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/image-pipeline")
            .RequireAuthorization()
            .WithTags("Image Pipeline");

        group.MapPost("/upload", async (
            HttpRequest request,
            ICurrentUser currentUser,
            IBlobStorageService blobStorage,
            PortfolioDbContext db,
            IImageProcessingQueue queue) =>
        {
            if (!currentUser.IsAdmin)
                return Results.Forbid();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Multipart form data required." });

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("image");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No image file provided." });

            // Validate content type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/tiff" };
            if (!allowedTypes.Contains(file.ContentType?.ToLowerInvariant()))
                return Results.BadRequest(new { error = "Unsupported image format. Allowed: JPEG, PNG, WebP, TIFF." });

            var slug = form["slug"].ToString();
            if (string.IsNullOrWhiteSpace(slug))
                return Results.BadRequest(new { error = "Slug is required." });

            // Check slug uniqueness
            if (db.Photos.Any(p => p.Slug == slug))
                return Results.Conflict(new { error = $"Photo with slug '{slug}' already exists." });

            var photoId = Guid.NewGuid();
            var extension = Path.GetExtension(file.FileName) ?? ".jpg";
            var blobName = $"{photoId}/original{extension}";

            // Upload original to blob storage
            using var stream = file.OpenReadStream();
            var blobUrl = await blobStorage.UploadAsync(
                BlobStorageService.OriginalsContainer, blobName, stream, file.ContentType!);

            // Create photo record
            var photo = new Photo
            {
                Id = photoId,
                TitleSk = form["titleSk"].ToString().NullIfEmpty(),
                TitleEn = form["titleEn"].ToString().NullIfEmpty(),
                Slug = slug,
                DescriptionSk = form["descriptionSk"].ToString().NullIfEmpty(),
                DescriptionEn = form["descriptionEn"].ToString().NullIfEmpty(),
                AltTextSk = form["altTextSk"].ToString().NullIfEmpty(),
                AltTextEn = form["altTextEn"].ToString().NullIfEmpty(),
                Location = form["location"].ToString().NullIfEmpty(),
                OriginalBlobUrl = blobUrl,
                IsPublished = false
            };

            db.Photos.Add(photo);
            await db.SaveChangesAsync();

            // Queue for background processing
            await queue.EnqueueAsync(photoId);

            return Results.Created($"/api/image-pipeline/photos/{photoId}", new
            {
                id = photoId,
                slug = photo.Slug,
                originalBlobUrl = blobUrl,
                status = "processing"
            });
        })
        .DisableAntiforgery();

        group.MapGet("/photos/{id:guid}/status", async (
            Guid id,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin)
                return Results.Forbid();

            var photo = await db.Photos.FindAsync(id);
            if (photo is null)
                return Results.NotFound();

            var variantCount = db.PhotoVariants.Count(v => v.PhotoId == id);
            var isProcessed = variantCount > 0;

            return Results.Ok(new
            {
                id = photo.Id,
                slug = photo.Slug,
                isProcessed,
                variantCount,
                width = photo.Width,
                height = photo.Height,
                dominantColor = photo.DominantColor,
                blurhash = photo.Blurhash,
                cameraSettings = photo.CameraSettings
            });
        });
    }

    private static string? NullIfEmpty(this string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
