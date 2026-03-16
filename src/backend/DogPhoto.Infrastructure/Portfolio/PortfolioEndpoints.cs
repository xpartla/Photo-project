using DogPhoto.Infrastructure.Persistence.Portfolio;
using DogPhoto.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Portfolio;

public static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portfolio").WithTags("Portfolio");

        // ── Public endpoints ───────────────────────────────────────────

        group.MapGet("/photos", async (
            PortfolioDbContext db,
            int? page,
            int? size,
            string? tag,
            string? collection,
            string? lang) =>
        {
            var pageNum = Math.Max(page ?? 1, 1);
            var pageSize = Math.Clamp(size ?? 20, 1, 100);

            var query = db.Photos
                .Where(p => p.IsPublished)
                .Include(p => p.Variants)
                .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.CollectionPhotos).ThenInclude(cp => cp.Collection)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(p => p.PhotoTags.Any(pt => pt.Tag.Slug == tag));

            if (!string.IsNullOrWhiteSpace(collection))
                query = query.Where(p => p.CollectionPhotos.Any(cp => cp.Collection.Slug == collection));

            var totalCount = await query.CountAsync();
            var photos = await query
                .OrderBy(p => p.SortOrder)
                .ThenByDescending(p => p.CreatedAt)
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var l = lang ?? "sk";
            return Results.Ok(new
            {
                items = photos.Select(p => MapPhotoSummary(p, l)),
                page = pageNum,
                size = pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        group.MapGet("/photos/{slug}", async (
            string slug,
            PortfolioDbContext db,
            string? lang) =>
        {
            var photo = await db.Photos
                .Where(p => p.IsPublished)
                .Include(p => p.Variants)
                .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.CollectionPhotos).ThenInclude(cp => cp.Collection)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            if (photo is null)
                return Results.NotFound();

            var l = lang ?? "sk";

            // Get related photos (same tags, excluding current)
            var tagIds = photo.PhotoTags.Select(pt => pt.TagId).ToList();
            var relatedPhotos = tagIds.Count > 0
                ? await db.Photos
                    .Where(p => p.IsPublished && p.Id != photo.Id)
                    .Where(p => p.PhotoTags.Any(pt => tagIds.Contains(pt.TagId)))
                    .Include(p => p.Variants)
                    .OrderBy(p => p.SortOrder)
                    .Take(6)
                    .ToListAsync()
                : [];

            return Results.Ok(new
            {
                id = photo.Id,
                slug = photo.Slug,
                title = l == "en" ? photo.TitleEn ?? photo.TitleSk : photo.TitleSk ?? photo.TitleEn,
                titleSk = photo.TitleSk,
                titleEn = photo.TitleEn,
                description = l == "en" ? photo.DescriptionEn ?? photo.DescriptionSk : photo.DescriptionSk ?? photo.DescriptionEn,
                descriptionSk = photo.DescriptionSk,
                descriptionEn = photo.DescriptionEn,
                altText = l == "en" ? photo.AltTextEn ?? photo.AltTextSk : photo.AltTextSk ?? photo.AltTextEn,
                altTextSk = photo.AltTextSk,
                altTextEn = photo.AltTextEn,
                cameraSettings = photo.CameraSettings,
                location = photo.Location,
                shotDate = photo.ShotDate,
                width = photo.Width,
                height = photo.Height,
                dominantColor = photo.DominantColor,
                blurhash = photo.Blurhash,
                variants = photo.Variants.Select(v => new
                {
                    width = v.Width,
                    height = v.Height,
                    format = v.Format,
                    blobUrl = v.BlobUrl,
                    sizeBytes = v.SizeBytes
                }),
                tags = photo.PhotoTags.Select(pt => new
                {
                    slug = pt.Tag.Slug,
                    name = l == "en" ? pt.Tag.NameEn : pt.Tag.NameSk
                }),
                collections = photo.CollectionPhotos.Select(cp => new
                {
                    slug = cp.Collection.Slug,
                    name = l == "en" ? cp.Collection.NameEn : cp.Collection.NameSk
                }),
                relatedPhotos = relatedPhotos.Select(rp => MapPhotoSummary(rp, l))
            });
        });

        group.MapGet("/collections", async (
            PortfolioDbContext db,
            string? lang) =>
        {
            var collections = await db.Collections
                .Include(c => c.CollectionPhotos)
                    .ThenInclude(cp => cp.Photo)
                        .ThenInclude(p => p.Variants)
                .OrderBy(c => c.SortOrder)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync();

            var l = lang ?? "sk";
            return Results.Ok(collections.Select(c =>
            {
                var coverPhoto = c.CoverPhotoId.HasValue
                    ? c.CollectionPhotos.FirstOrDefault(cp => cp.PhotoId == c.CoverPhotoId)?.Photo
                    : c.CollectionPhotos.OrderBy(cp => cp.SortOrder).FirstOrDefault()?.Photo;

                return new
                {
                    id = c.Id,
                    slug = c.Slug,
                    name = l == "en" ? c.NameEn : c.NameSk,
                    nameSk = c.NameSk,
                    nameEn = c.NameEn,
                    description = l == "en" ? c.DescriptionEn ?? c.DescriptionSk : c.DescriptionSk ?? c.DescriptionEn,
                    photoCount = c.CollectionPhotos.Count(cp => cp.Photo.IsPublished),
                    coverPhoto = coverPhoto is not null ? MapPhotoSummary(coverPhoto, l) : null
                };
            }));
        });

        group.MapGet("/collections/{slug}", async (
            string slug,
            PortfolioDbContext db,
            string? lang,
            int? page,
            int? size) =>
        {
            var collection = await db.Collections
                .Include(c => c.CollectionPhotos.OrderBy(cp => cp.SortOrder))
                    .ThenInclude(cp => cp.Photo)
                        .ThenInclude(p => p.Variants)
                .Include(c => c.CollectionPhotos)
                    .ThenInclude(cp => cp.Photo)
                        .ThenInclude(p => p.PhotoTags)
                            .ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(c => c.Slug == slug);

            if (collection is null)
                return Results.NotFound();

            var l = lang ?? "sk";
            var pageNum = Math.Max(page ?? 1, 1);
            var pageSize = Math.Clamp(size ?? 20, 1, 100);

            var publishedPhotos = collection.CollectionPhotos
                .Where(cp => cp.Photo.IsPublished)
                .ToList();

            var totalCount = publishedPhotos.Count;
            var pagedPhotos = publishedPhotos
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Results.Ok(new
            {
                id = collection.Id,
                slug = collection.Slug,
                name = l == "en" ? collection.NameEn : collection.NameSk,
                nameSk = collection.NameSk,
                nameEn = collection.NameEn,
                description = l == "en" ? collection.DescriptionEn ?? collection.DescriptionSk : collection.DescriptionSk ?? collection.DescriptionEn,
                descriptionSk = collection.DescriptionSk,
                descriptionEn = collection.DescriptionEn,
                photos = new
                {
                    items = pagedPhotos.Select(cp => MapPhotoSummary(cp.Photo, l)),
                    page = pageNum,
                    size = pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            });
        });

        // ── Admin endpoints ────────────────────────────────────────────

        var admin = app.MapGroup("/api/portfolio").WithTags("Portfolio").RequireAuthorization();

        admin.MapPost("/photos", async (
            CreatePhotoRequest request,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            // This endpoint is for creating/updating photo metadata.
            // Image upload goes through /api/image-pipeline/upload.
            var photo = await db.Photos.FirstOrDefaultAsync(p => p.Slug == request.Slug);
            if (photo is null)
                return Results.NotFound(new { error = $"Photo with slug '{request.Slug}' not found. Upload via /api/image-pipeline/upload first." });

            photo.TitleSk = request.TitleSk ?? photo.TitleSk;
            photo.TitleEn = request.TitleEn ?? photo.TitleEn;
            photo.DescriptionSk = request.DescriptionSk ?? photo.DescriptionSk;
            photo.DescriptionEn = request.DescriptionEn ?? photo.DescriptionEn;
            photo.AltTextSk = request.AltTextSk ?? photo.AltTextSk;
            photo.AltTextEn = request.AltTextEn ?? photo.AltTextEn;
            photo.Location = request.Location ?? photo.Location;
            photo.IsPublished = request.IsPublished ?? photo.IsPublished;
            photo.SortOrder = request.SortOrder ?? photo.SortOrder;
            photo.UpdatedAt = DateTime.UtcNow;

            // Handle tags
            if (request.TagSlugs is not null)
            {
                var existingTags = await db.Tags
                    .Where(t => request.TagSlugs.Contains(t.Slug))
                    .ToListAsync();

                // Remove old tags
                var currentTags = await db.Set<PhotoTag>()
                    .Where(pt => pt.PhotoId == photo.Id)
                    .ToListAsync();
                db.Set<PhotoTag>().RemoveRange(currentTags);

                // Add new tags
                foreach (var tag in existingTags)
                {
                    db.Set<PhotoTag>().Add(new PhotoTag { PhotoId = photo.Id, TagId = tag.Id });
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { id = photo.Id, slug = photo.Slug });
        });

        admin.MapPut("/photos/{id:guid}", async (
            Guid id,
            UpdatePhotoRequest request,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var photo = await db.Photos
                .Include(p => p.PhotoTags)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (photo is null) return Results.NotFound();

            if (request.TitleSk is not null) photo.TitleSk = request.TitleSk;
            if (request.TitleEn is not null) photo.TitleEn = request.TitleEn;
            if (request.DescriptionSk is not null) photo.DescriptionSk = request.DescriptionSk;
            if (request.DescriptionEn is not null) photo.DescriptionEn = request.DescriptionEn;
            if (request.AltTextSk is not null) photo.AltTextSk = request.AltTextSk;
            if (request.AltTextEn is not null) photo.AltTextEn = request.AltTextEn;
            if (request.Location is not null) photo.Location = request.Location;
            if (request.IsPublished.HasValue) photo.IsPublished = request.IsPublished.Value;
            if (request.SortOrder.HasValue) photo.SortOrder = request.SortOrder.Value;
            photo.UpdatedAt = DateTime.UtcNow;

            if (request.TagSlugs is not null)
            {
                var existingTags = await db.Tags
                    .Where(t => request.TagSlugs.Contains(t.Slug))
                    .ToListAsync();

                db.Set<PhotoTag>().RemoveRange(photo.PhotoTags);

                foreach (var tag in existingTags)
                {
                    db.Set<PhotoTag>().Add(new PhotoTag { PhotoId = photo.Id, TagId = tag.Id });
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { id = photo.Id, slug = photo.Slug });
        });

        admin.MapDelete("/photos/{id:guid}", async (
            Guid id,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var photo = await db.Photos.FindAsync(id);
            if (photo is null) return Results.NotFound();

            photo.DeletedAt = DateTime.UtcNow;
            photo.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── Tags (Admin) ──────────────────────────────────────────────

        admin.MapPost("/tags", async (
            CreateTagRequest request,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (await db.Tags.AnyAsync(t => t.Slug == request.Slug))
                return Results.Conflict(new { error = $"Tag with slug '{request.Slug}' already exists." });

            var tag = new Tag
            {
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                Slug = request.Slug
            };

            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            return Results.Created($"/api/portfolio/tags/{tag.Slug}", new { id = tag.Id, slug = tag.Slug });
        });

        group.MapGet("/tags", async (PortfolioDbContext db, string? lang) =>
        {
            var tags = await db.Tags
                .Include(t => t.PhotoTags)
                .OrderBy(t => t.NameSk)
                .ToListAsync();

            var l = lang ?? "sk";
            return Results.Ok(tags.Select(t => new
            {
                slug = t.Slug,
                name = l == "en" ? t.NameEn : t.NameSk,
                photoCount = t.PhotoTags.Count
            }));
        });

        // ── Collections (Admin) ────────────────────────────────────────

        admin.MapPost("/collections", async (
            CreateCollectionRequest request,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (await db.Collections.AnyAsync(c => c.Slug == request.Slug))
                return Results.Conflict(new { error = $"Collection with slug '{request.Slug}' already exists." });

            var collection = new Collection
            {
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                Slug = request.Slug,
                DescriptionSk = request.DescriptionSk,
                DescriptionEn = request.DescriptionEn,
                CoverPhotoId = request.CoverPhotoId,
                SortOrder = request.SortOrder ?? 0
            };

            db.Collections.Add(collection);
            await db.SaveChangesAsync();

            // Add photos to collection
            if (request.PhotoIds is { Count: > 0 })
            {
                for (var i = 0; i < request.PhotoIds.Count; i++)
                {
                    db.Set<CollectionPhoto>().Add(new CollectionPhoto
                    {
                        CollectionId = collection.Id,
                        PhotoId = request.PhotoIds[i],
                        SortOrder = i
                    });
                }
                await db.SaveChangesAsync();
            }

            return Results.Created($"/api/portfolio/collections/{collection.Slug}",
                new { id = collection.Id, slug = collection.Slug });
        });

        admin.MapPut("/collections/{id:guid}", async (
            Guid id,
            UpdateCollectionRequest request,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var collection = await db.Collections
                .Include(c => c.CollectionPhotos)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (collection is null) return Results.NotFound();

            if (request.NameSk is not null) collection.NameSk = request.NameSk;
            if (request.NameEn is not null) collection.NameEn = request.NameEn;
            if (request.DescriptionSk is not null) collection.DescriptionSk = request.DescriptionSk;
            if (request.DescriptionEn is not null) collection.DescriptionEn = request.DescriptionEn;
            if (request.CoverPhotoId.HasValue) collection.CoverPhotoId = request.CoverPhotoId;
            if (request.SortOrder.HasValue) collection.SortOrder = request.SortOrder.Value;
            collection.UpdatedAt = DateTime.UtcNow;

            if (request.PhotoIds is not null)
            {
                db.Set<CollectionPhoto>().RemoveRange(collection.CollectionPhotos);
                for (var i = 0; i < request.PhotoIds.Count; i++)
                {
                    db.Set<CollectionPhoto>().Add(new CollectionPhoto
                    {
                        CollectionId = collection.Id,
                        PhotoId = request.PhotoIds[i],
                        SortOrder = i
                    });
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { id = collection.Id, slug = collection.Slug });
        });

        admin.MapDelete("/collections/{id:guid}", async (
            Guid id,
            ICurrentUser currentUser,
            PortfolioDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var collection = await db.Collections.FindAsync(id);
            if (collection is null) return Results.NotFound();

            collection.DeletedAt = DateTime.UtcNow;
            collection.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static object MapPhotoSummary(Photo photo, string lang)
    {
        return new
        {
            id = photo.Id,
            slug = photo.Slug,
            title = lang == "en" ? photo.TitleEn ?? photo.TitleSk : photo.TitleSk ?? photo.TitleEn,
            altText = lang == "en" ? photo.AltTextEn ?? photo.AltTextSk : photo.AltTextSk ?? photo.AltTextEn,
            width = photo.Width,
            height = photo.Height,
            dominantColor = photo.DominantColor,
            blurhash = photo.Blurhash,
            variants = photo.Variants.Select(v => new
            {
                width = v.Width,
                height = v.Height,
                format = v.Format,
                blobUrl = v.BlobUrl
            }),
            tags = photo.PhotoTags.Select(pt => new
            {
                slug = pt.Tag.Slug,
                name = lang == "en" ? pt.Tag.NameEn : pt.Tag.NameSk
            })
        };
    }
}

// ── Request DTOs ───────────────────────────────────────────────────

public record CreatePhotoRequest(
    string Slug,
    string? TitleSk,
    string? TitleEn,
    string? DescriptionSk,
    string? DescriptionEn,
    string? AltTextSk,
    string? AltTextEn,
    string? Location,
    bool? IsPublished,
    int? SortOrder,
    List<string>? TagSlugs);

public record UpdatePhotoRequest(
    string? TitleSk,
    string? TitleEn,
    string? DescriptionSk,
    string? DescriptionEn,
    string? AltTextSk,
    string? AltTextEn,
    string? Location,
    bool? IsPublished,
    int? SortOrder,
    List<string>? TagSlugs);

public record CreateTagRequest(
    string NameSk,
    string NameEn,
    string Slug);

public record CreateCollectionRequest(
    string NameSk,
    string NameEn,
    string Slug,
    string? DescriptionSk,
    string? DescriptionEn,
    Guid? CoverPhotoId,
    int? SortOrder,
    List<Guid>? PhotoIds);

public record UpdateCollectionRequest(
    string? NameSk,
    string? NameEn,
    string? DescriptionSk,
    string? DescriptionEn,
    Guid? CoverPhotoId,
    int? SortOrder,
    List<Guid>? PhotoIds);
