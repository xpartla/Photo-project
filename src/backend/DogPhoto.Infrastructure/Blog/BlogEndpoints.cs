using System.Text;
using System.Xml;
using DogPhoto.Infrastructure.Persistence.Blog;
using DogPhoto.SharedKernel.Auth;
using Markdig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Blog;

public static class BlogEndpoints
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public static void MapBlogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/blog").WithTags("Blog");

        // ── Public ─────────────────────────────────────────────────────

        group.MapGet("/posts", async (
            BlogDbContext db,
            int? page,
            int? size,
            string? category,
            string? tag,
            string? q,
            string? lang,
            bool? includeDrafts,
            ICurrentUser currentUser) =>
        {
            var pageNum = Math.Max(page ?? 1, 1);
            var pageSize = Math.Clamp(size ?? 10, 1, 50);
            var l = lang ?? "sk";

            var query = db.Posts
                .Include(p => p.PostCategories).ThenInclude(pc => pc.Category)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .AsQueryable();

            // Drafts and scheduled posts are admin-only
            var now = DateTime.UtcNow;
            if (!(includeDrafts == true && currentUser.IsAdmin))
            {
                query = query.Where(p => p.Status == "Published" && p.PublishedAt != null && p.PublishedAt <= now);
            }

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.PostCategories.Any(pc => pc.Category.Slug == category));

            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.TitleSk, pattern) ||
                    EF.Functions.ILike(p.TitleEn, pattern) ||
                    (p.ExcerptSk != null && EF.Functions.ILike(p.ExcerptSk, pattern)) ||
                    (p.ExcerptEn != null && EF.Functions.ILike(p.ExcerptEn, pattern)) ||
                    (p.ContentMarkdownSk != null && EF.Functions.ILike(p.ContentMarkdownSk, pattern)) ||
                    (p.ContentMarkdownEn != null && EF.Functions.ILike(p.ContentMarkdownEn, pattern)));
            }

            var totalCount = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(new
            {
                items = posts.Select(p => MapPostSummary(p, l)),
                page = pageNum,
                size = pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        group.MapGet("/posts/{slug}", async (
            string slug,
            BlogDbContext db,
            string? lang,
            ICurrentUser currentUser) =>
        {
            var post = await db.Posts
                .Include(p => p.PostCategories).ThenInclude(pc => pc.Category)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            if (post is null) return Results.NotFound();

            // Non-admins can only see published posts with a past publishedAt
            var now = DateTime.UtcNow;
            var isVisible = post.Status == "Published" && post.PublishedAt != null && post.PublishedAt <= now;
            if (!isVisible && !currentUser.IsAdmin) return Results.NotFound();

            var l = lang ?? "sk";

            // Related posts — share ≥1 tag, published, exclude current
            var tagIds = post.PostTags.Select(pt => pt.TagId).ToList();
            var related = tagIds.Count > 0
                ? await db.Posts
                    .Where(p => p.Id != post.Id && p.Status == "Published" && p.PublishedAt != null && p.PublishedAt <= now)
                    .Where(p => p.PostTags.Any(pt => tagIds.Contains(pt.TagId)))
                    .OrderByDescending(p => p.PublishedAt)
                    .Take(3)
                    .ToListAsync()
                : [];

            return Results.Ok(MapPostDetail(post, l, related));
        });

        group.MapGet("/categories", async (BlogDbContext db, string? lang) =>
        {
            var l = lang ?? "sk";
            var now = DateTime.UtcNow;
            var cats = await db.Categories
                .Include(c => c.PostCategories).ThenInclude(pc => pc.Post)
                .OrderBy(c => l == "en" ? c.NameEn : c.NameSk)
                .ToListAsync();

            return Results.Ok(cats.Select(c => new
            {
                slug = c.Slug,
                name = l == "en" ? c.NameEn : c.NameSk,
                nameSk = c.NameSk,
                nameEn = c.NameEn,
                postCount = c.PostCategories.Count(pc =>
                    pc.Post.Status == "Published" && pc.Post.PublishedAt != null && pc.Post.PublishedAt <= now)
            }));
        });

        group.MapGet("/tags", async (BlogDbContext db, string? lang) =>
        {
            var l = lang ?? "sk";
            var now = DateTime.UtcNow;
            var tags = await db.Tags
                .Include(t => t.PostTags).ThenInclude(pt => pt.Post)
                .OrderBy(t => l == "en" ? t.NameEn : t.NameSk)
                .ToListAsync();

            return Results.Ok(tags
                .Select(t => new
                {
                    slug = t.Slug,
                    name = l == "en" ? t.NameEn : t.NameSk,
                    nameSk = t.NameSk,
                    nameEn = t.NameEn,
                    postCount = t.PostTags.Count(pt =>
                        pt.Post.Status == "Published" && pt.Post.PublishedAt != null && pt.Post.PublishedAt <= now)
                })
                .Where(t => t.postCount > 0));
        });

        // ── RSS feed (public) ──────────────────────────────────────────
        app.MapGet("/rss.xml", async (BlogDbContext db, HttpContext ctx) =>
        {
            var now = DateTime.UtcNow;
            var posts = await db.Posts
                .Where(p => p.Status == "Published" && p.PublishedAt != null && p.PublishedAt <= now)
                .OrderByDescending(p => p.PublishedAt)
                .Take(50)
                .ToListAsync();

            var scheme = ctx.Request.Scheme;
            var host = ctx.Request.Host.Value;
            var siteUrl = $"{scheme}://{host}";

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("rss");
                writer.WriteAttributeString("version", "2.0");
                writer.WriteStartElement("channel");
                writer.WriteElementString("title", "PartlPhoto Blog");
                writer.WriteElementString("link", siteUrl);
                writer.WriteElementString("description", "Fine art 35mm film & dog photography — stories, tips, locations.");
                writer.WriteElementString("language", "sk");

                foreach (var p in posts)
                {
                    writer.WriteStartElement("item");
                    writer.WriteElementString("title", p.TitleSk);
                    writer.WriteElementString("link", $"{siteUrl}/sk/blog/{p.Slug}");
                    writer.WriteElementString("guid", p.Id.ToString());
                    writer.WriteElementString("pubDate", (p.PublishedAt ?? p.CreatedAt).ToString("R"));
                    writer.WriteElementString("description", p.ExcerptSk ?? "");
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return Results.Content(sb.ToString(), "application/rss+xml; charset=utf-8");
        });

        // ── Admin endpoints ────────────────────────────────────────────
        var admin = app.MapGroup("/api/blog").WithTags("Blog").RequireAuthorization();

        admin.MapPost("/posts", async (
            CreatePostRequest request,
            ICurrentUser currentUser,
            BlogDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (await db.Posts.AnyAsync(p => p.Slug == request.Slug))
                return Results.Conflict(new { error = $"Post with slug '{request.Slug}' already exists." });

            var post = new Post
            {
                Slug = request.Slug,
                TitleSk = request.TitleSk,
                TitleEn = request.TitleEn,
                ExcerptSk = request.ExcerptSk,
                ExcerptEn = request.ExcerptEn,
                ContentMarkdownSk = request.ContentMarkdownSk,
                ContentMarkdownEn = request.ContentMarkdownEn,
                ContentHtmlSk = RenderMarkdown(request.ContentMarkdownSk),
                ContentHtmlEn = RenderMarkdown(request.ContentMarkdownEn),
                FeaturedImageUrl = request.FeaturedImageUrl,
                Author = request.Author,
                Status = NormalizeStatus(request.Status),
                PublishedAt = ResolvePublishedAt(request.Status, request.PublishedAt),
                MetaTitleSk = request.MetaTitleSk,
                MetaTitleEn = request.MetaTitleEn,
                MetaDescriptionSk = request.MetaDescriptionSk,
                MetaDescriptionEn = request.MetaDescriptionEn,
            };

            db.Posts.Add(post);
            await db.SaveChangesAsync();

            await AssignCategoriesAsync(db, post.Id, request.CategorySlugs);
            await AssignTagsAsync(db, post.Id, request.TagSlugs);
            await db.SaveChangesAsync();

            return Results.Created($"/api/blog/posts/{post.Slug}", new { id = post.Id, slug = post.Slug });
        });

        admin.MapPut("/posts/{id:guid}", async (
            Guid id,
            UpdatePostRequest request,
            ICurrentUser currentUser,
            BlogDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var post = await db.Posts
                .Include(p => p.PostCategories)
                .Include(p => p.PostTags)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post is null) return Results.NotFound();

            if (request.TitleSk is not null) post.TitleSk = request.TitleSk;
            if (request.TitleEn is not null) post.TitleEn = request.TitleEn;
            if (request.Slug is not null) post.Slug = request.Slug;
            if (request.ExcerptSk is not null) post.ExcerptSk = request.ExcerptSk;
            if (request.ExcerptEn is not null) post.ExcerptEn = request.ExcerptEn;
            if (request.ContentMarkdownSk is not null)
            {
                post.ContentMarkdownSk = request.ContentMarkdownSk;
                post.ContentHtmlSk = RenderMarkdown(request.ContentMarkdownSk);
            }
            if (request.ContentMarkdownEn is not null)
            {
                post.ContentMarkdownEn = request.ContentMarkdownEn;
                post.ContentHtmlEn = RenderMarkdown(request.ContentMarkdownEn);
            }
            if (request.FeaturedImageUrl is not null) post.FeaturedImageUrl = request.FeaturedImageUrl;
            if (request.Author is not null) post.Author = request.Author;
            if (request.Status is not null)
            {
                post.Status = NormalizeStatus(request.Status);
                post.PublishedAt = ResolvePublishedAt(request.Status, request.PublishedAt ?? post.PublishedAt);
            }
            else if (request.PublishedAt.HasValue)
            {
                post.PublishedAt = request.PublishedAt;
            }
            if (request.MetaTitleSk is not null) post.MetaTitleSk = request.MetaTitleSk;
            if (request.MetaTitleEn is not null) post.MetaTitleEn = request.MetaTitleEn;
            if (request.MetaDescriptionSk is not null) post.MetaDescriptionSk = request.MetaDescriptionSk;
            if (request.MetaDescriptionEn is not null) post.MetaDescriptionEn = request.MetaDescriptionEn;

            post.UpdatedAt = DateTime.UtcNow;

            if (request.CategorySlugs is not null)
            {
                db.Set<PostCategory>().RemoveRange(post.PostCategories);
                await db.SaveChangesAsync();
                await AssignCategoriesAsync(db, post.Id, request.CategorySlugs);
            }

            if (request.TagSlugs is not null)
            {
                db.Set<PostTag>().RemoveRange(post.PostTags);
                await db.SaveChangesAsync();
                await AssignTagsAsync(db, post.Id, request.TagSlugs);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { id = post.Id, slug = post.Slug });
        });

        admin.MapDelete("/posts/{id:guid}", async (
            Guid id,
            ICurrentUser currentUser,
            BlogDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var post = await db.Posts.FindAsync(id);
            if (post is null) return Results.NotFound();

            post.DeletedAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Admin: fetch post by id (for edit page — returns raw markdown + drafts)
        admin.MapGet("/posts/by-id/{id:guid}", async (
            Guid id,
            ICurrentUser currentUser,
            BlogDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var post = await db.Posts
                .Include(p => p.PostCategories).ThenInclude(pc => pc.Category)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post is null) return Results.NotFound();

            return Results.Ok(new
            {
                id = post.Id,
                slug = post.Slug,
                titleSk = post.TitleSk,
                titleEn = post.TitleEn,
                excerptSk = post.ExcerptSk,
                excerptEn = post.ExcerptEn,
                contentMarkdownSk = post.ContentMarkdownSk,
                contentMarkdownEn = post.ContentMarkdownEn,
                featuredImageUrl = post.FeaturedImageUrl,
                author = post.Author,
                status = post.Status,
                publishedAt = post.PublishedAt,
                metaTitleSk = post.MetaTitleSk,
                metaTitleEn = post.MetaTitleEn,
                metaDescriptionSk = post.MetaDescriptionSk,
                metaDescriptionEn = post.MetaDescriptionEn,
                categorySlugs = post.PostCategories.Select(pc => pc.Category.Slug).ToList(),
                tagSlugs = post.PostTags.Select(pt => pt.Tag.Slug).ToList(),
                createdAt = post.CreatedAt,
                updatedAt = post.UpdatedAt
            });
        });

        // ── Admin: categories ─────────────────────────────────────────
        admin.MapPost("/categories", async (
            CreateCategoryRequest request,
            ICurrentUser currentUser,
            BlogDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (await db.Categories.AnyAsync(c => c.Slug == request.Slug))
                return Results.Conflict(new { error = $"Category with slug '{request.Slug}' already exists." });

            var cat = new Category
            {
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                Slug = request.Slug,
            };

            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            return Results.Created($"/api/blog/categories/{cat.Slug}", new { id = cat.Id, slug = cat.Slug });
        });

        // ── Admin: tags ────────────────────────────────────────────────
        admin.MapPost("/tags", async (
            CreateBlogTagRequest request,
            ICurrentUser currentUser,
            BlogDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (await db.Tags.AnyAsync(t => t.Slug == request.Slug))
                return Results.Conflict(new { error = $"Tag with slug '{request.Slug}' already exists." });

            var tag = new BlogTag
            {
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                Slug = request.Slug,
            };

            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            return Results.Created($"/api/blog/tags/{tag.Slug}", new { id = tag.Id, slug = tag.Slug });
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string? RenderMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return null;
        return Markdown.ToHtml(markdown, MarkdownPipeline);
    }

    private static string NormalizeStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "published" => "Published",
            "scheduled" => "Scheduled",
            _ => "Draft"
        };
    }

    private static DateTime? ResolvePublishedAt(string? status, DateTime? publishedAt)
    {
        var s = NormalizeStatus(status);
        return s switch
        {
            "Published" => publishedAt ?? DateTime.UtcNow,
            "Scheduled" => publishedAt,
            _ => null
        };
    }

    private static async Task AssignCategoriesAsync(BlogDbContext db, Guid postId, List<string>? slugs)
    {
        if (slugs is null || slugs.Count == 0) return;
        var cats = await db.Categories.Where(c => slugs.Contains(c.Slug)).ToListAsync();
        foreach (var cat in cats)
            db.Set<PostCategory>().Add(new PostCategory { PostId = postId, CategoryId = cat.Id });
    }

    private static async Task AssignTagsAsync(BlogDbContext db, Guid postId, List<string>? slugs)
    {
        if (slugs is null || slugs.Count == 0) return;
        var tags = await db.Tags.Where(t => slugs.Contains(t.Slug)).ToListAsync();
        foreach (var tag in tags)
            db.Set<PostTag>().Add(new PostTag { PostId = postId, TagId = tag.Id });
    }

    private static object MapPostSummary(Post post, string lang)
    {
        return new
        {
            id = post.Id,
            slug = post.Slug,
            title = lang == "en" ? post.TitleEn : post.TitleSk,
            titleSk = post.TitleSk,
            titleEn = post.TitleEn,
            excerpt = lang == "en" ? post.ExcerptEn : post.ExcerptSk,
            featuredImageUrl = post.FeaturedImageUrl,
            author = post.Author,
            status = post.Status,
            publishedAt = post.PublishedAt,
            createdAt = post.CreatedAt,
            categories = post.PostCategories.Select(pc => new
            {
                slug = pc.Category.Slug,
                name = lang == "en" ? pc.Category.NameEn : pc.Category.NameSk
            }),
            tags = post.PostTags.Select(pt => new
            {
                slug = pt.Tag.Slug,
                name = lang == "en" ? pt.Tag.NameEn : pt.Tag.NameSk
            })
        };
    }

    private static object MapPostDetail(Post post, string lang, List<Post> related)
    {
        var words = ((lang == "en" ? post.ContentMarkdownEn : post.ContentMarkdownSk) ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var readingMinutes = Math.Max(1, (int)Math.Ceiling(words / 200.0));

        return new
        {
            id = post.Id,
            slug = post.Slug,
            title = lang == "en" ? post.TitleEn : post.TitleSk,
            titleSk = post.TitleSk,
            titleEn = post.TitleEn,
            excerpt = lang == "en" ? post.ExcerptEn : post.ExcerptSk,
            excerptSk = post.ExcerptSk,
            excerptEn = post.ExcerptEn,
            contentHtml = lang == "en" ? post.ContentHtmlEn : post.ContentHtmlSk,
            contentHtmlSk = post.ContentHtmlSk,
            contentHtmlEn = post.ContentHtmlEn,
            featuredImageUrl = post.FeaturedImageUrl,
            author = post.Author,
            status = post.Status,
            publishedAt = post.PublishedAt,
            readingMinutes,
            metaTitle = lang == "en" ? post.MetaTitleEn : post.MetaTitleSk,
            metaDescription = lang == "en" ? post.MetaDescriptionEn : post.MetaDescriptionSk,
            categories = post.PostCategories.Select(pc => new
            {
                slug = pc.Category.Slug,
                name = lang == "en" ? pc.Category.NameEn : pc.Category.NameSk
            }),
            tags = post.PostTags.Select(pt => new
            {
                slug = pt.Tag.Slug,
                name = lang == "en" ? pt.Tag.NameEn : pt.Tag.NameSk
            }),
            relatedPosts = related.Select(r => new
            {
                slug = r.Slug,
                title = lang == "en" ? r.TitleEn : r.TitleSk,
                excerpt = lang == "en" ? r.ExcerptEn : r.ExcerptSk,
                featuredImageUrl = r.FeaturedImageUrl,
                publishedAt = r.PublishedAt
            })
        };
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────

public record CreatePostRequest(
    string Slug,
    string TitleSk,
    string TitleEn,
    string? ExcerptSk,
    string? ExcerptEn,
    string? ContentMarkdownSk,
    string? ContentMarkdownEn,
    string? FeaturedImageUrl,
    string? Author,
    string? Status,
    DateTime? PublishedAt,
    string? MetaTitleSk,
    string? MetaTitleEn,
    string? MetaDescriptionSk,
    string? MetaDescriptionEn,
    List<string>? CategorySlugs,
    List<string>? TagSlugs);

public record UpdatePostRequest(
    string? Slug,
    string? TitleSk,
    string? TitleEn,
    string? ExcerptSk,
    string? ExcerptEn,
    string? ContentMarkdownSk,
    string? ContentMarkdownEn,
    string? FeaturedImageUrl,
    string? Author,
    string? Status,
    DateTime? PublishedAt,
    string? MetaTitleSk,
    string? MetaTitleEn,
    string? MetaDescriptionSk,
    string? MetaDescriptionEn,
    List<string>? CategorySlugs,
    List<string>? TagSlugs);

public record CreateCategoryRequest(string NameSk, string NameEn, string Slug);
public record CreateBlogTagRequest(string NameSk, string NameEn, string Slug);
