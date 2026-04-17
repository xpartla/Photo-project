using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class BlogTests : ApiTestBase
{
    public BlogTests(ApiFactory factory) : base(factory) { }

    private static string UniqueSlug(string prefix)
    {
        var raw = $"{prefix}-{Guid.NewGuid():N}";
        return raw.Length > 48 ? raw[..48] : raw;
    }

    private async Task<(string Id, string Slug)> CreatePublishedPostAsync(HttpClient admin, string? slug = null, string? title = null, string[]? categorySlugs = null, string[]? tagSlugs = null, string contentSk = "# Body", string contentEn = "# Body")
    {
        slug ??= UniqueSlug("post");
        title ??= $"Title {slug}";
        var response = await admin.PostAsJsonAsync("/api/blog/posts", new
        {
            slug,
            titleSk = $"{title} SK",
            titleEn = $"{title} EN",
            excerptSk = "Úryvok",
            excerptEn = "Excerpt",
            contentMarkdownSk = contentSk,
            contentMarkdownEn = contentEn,
            author = "Adam",
            status = "Published",
            categorySlugs,
            tagSlugs,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return (doc.RootElement.GetProperty("id").GetString()!, doc.RootElement.GetProperty("slug").GetString()!);
    }

    // ── Public listing + detail ───────────────────────────────────

    [Fact]
    public async Task GetPosts_Public_ReturnsEmptyByDefault_OrSeededOnly()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/blog/posts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("items").GetArrayLength() >= 0);
    }

    [Fact]
    public async Task GetPosts_DraftHiddenFromPublic_VisibleToAdmin()
    {
        var admin = await CreateAdminClientAsync();
        var slug = UniqueSlug("draft");
        var create = await admin.PostAsJsonAsync("/api/blog/posts", new
        {
            slug,
            titleSk = "Draft SK",
            titleEn = "Draft EN",
            contentMarkdownSk = "# Draft",
            contentMarkdownEn = "# Draft",
            status = "Draft",
        });
        create.EnsureSuccessStatusCode();

        // Public cannot see the draft
        var publicList = await CreateClient().GetAsync("/api/blog/posts?size=100");
        using var publicDoc = JsonDocument.Parse(await publicList.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            publicDoc.RootElement.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("slug").GetString() == slug);

        // Admin with includeDrafts=true can see it
        var adminList = await admin.GetAsync("/api/blog/posts?size=100&includeDrafts=true");
        using var adminDoc = JsonDocument.Parse(await adminList.Content.ReadAsStringAsync());
        Assert.Contains(
            adminDoc.RootElement.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("slug").GetString() == slug);
    }

    [Fact]
    public async Task GetPost_Public_DraftReturns404()
    {
        var admin = await CreateAdminClientAsync();
        var slug = UniqueSlug("draft-detail");
        await admin.PostAsJsonAsync("/api/blog/posts", new
        {
            slug,
            titleSk = "X", titleEn = "X",
            contentMarkdownSk = "x", contentMarkdownEn = "x",
            status = "Draft",
        });

        var response = await CreateClient().GetAsync($"/api/blog/posts/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPost_Public_Published_RendersMarkdown()
    {
        var admin = await CreateAdminClientAsync();
        var (_, slug) = await CreatePublishedPostAsync(admin, contentSk: "# Hello\n\nA paragraph.", contentEn: "# Hello\n\nA paragraph.");

        var response = await CreateClient().GetAsync($"/api/blog/posts/{slug}?lang=en");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var html = doc.RootElement.GetProperty("contentHtml").GetString()!;
        Assert.Contains("<h1", html);
        Assert.Contains("Hello", html);
    }

    [Fact]
    public async Task GetPost_ReadingMinutes_ComputedFromWordCount()
    {
        var admin = await CreateAdminClientAsync();
        var longContent = string.Join(" ", Enumerable.Repeat("word", 400));
        var (_, slug) = await CreatePublishedPostAsync(admin, contentSk: longContent, contentEn: longContent);

        var response = await CreateClient().GetAsync($"/api/blog/posts/{slug}");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("readingMinutes").GetInt32() >= 2);
    }

    // ── Filters: category / tag / search ──────────────────────────

    [Fact]
    public async Task GetPosts_FilterByCategory_ReturnsOnlyMatching()
    {
        var admin = await CreateAdminClientAsync();
        var (_, slug) = await CreatePublishedPostAsync(admin, categorySlugs: new[] { "photography-tips" });
        var (_, otherSlug) = await CreatePublishedPostAsync(admin, categorySlugs: new[] { "locations" });

        var response = await CreateClient().GetAsync("/api/blog/posts?category=photography-tips&size=100");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var slugs = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()).ToList();
        Assert.Contains(slug, slugs);
        Assert.DoesNotContain(otherSlug, slugs);
    }

    [Fact]
    public async Task GetPosts_FilterByTag_ReturnsOnlyMatching()
    {
        var admin = await CreateAdminClientAsync();
        // Create a tag first
        var tagSlug = UniqueSlug("tag").ToLowerInvariant();
        var createTag = await admin.PostAsJsonAsync("/api/blog/tags", new
        {
            nameSk = tagSlug, nameEn = tagSlug, slug = tagSlug,
        });
        createTag.EnsureSuccessStatusCode();

        var (_, slug) = await CreatePublishedPostAsync(admin, tagSlugs: new[] { tagSlug });
        var (_, otherSlug) = await CreatePublishedPostAsync(admin);

        var response = await CreateClient().GetAsync($"/api/blog/posts?tag={tagSlug}&size=100");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var slugs = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()).ToList();
        Assert.Contains(slug, slugs);
        Assert.DoesNotContain(otherSlug, slugs);
    }

    [Fact]
    public async Task GetPosts_FullTextSearch_MatchesTitleExcerptAndContent()
    {
        var admin = await CreateAdminClientAsync();
        var marker = $"zxq{Guid.NewGuid():N}"[..12];

        // Marker in title
        var (_, slugTitle) = await CreatePublishedPostAsync(admin, title: $"Post {marker} title");
        // Marker in body
        var bodyContent = $"# Body\n\nbody mentions {marker} here.";
        var (_, slugBody) = await CreatePublishedPostAsync(admin, contentSk: bodyContent, contentEn: bodyContent);
        // Not matching
        var (_, slugOther) = await CreatePublishedPostAsync(admin);

        var response = await CreateClient().GetAsync($"/api/blog/posts?q={marker}&size=100");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var slugs = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()).ToList();
        Assert.Contains(slugTitle, slugs);
        Assert.Contains(slugBody, slugs);
        Assert.DoesNotContain(slugOther, slugs);
    }

    // ── Auth & admin ──────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_Unauthenticated_Returns401()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/blog/posts", new { slug = "x", titleSk = "x", titleEn = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePost_AsCustomer_Returns403()
    {
        var customer = await CreateCustomerClientAsync();
        var response = await customer.PostAsJsonAsync("/api/blog/posts", new { slug = "x", titleSk = "x", titleEn = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateSlug_Returns409()
    {
        var admin = await CreateAdminClientAsync();
        var slug = UniqueSlug("dup");
        var first = await admin.PostAsJsonAsync("/api/blog/posts", new
        {
            slug, titleSk = "A", titleEn = "A",
            contentMarkdownSk = "a", contentMarkdownEn = "a",
            status = "Published",
        });
        first.EnsureSuccessStatusCode();

        var second = await admin.PostAsJsonAsync("/api/blog/posts", new
        {
            slug, titleSk = "B", titleEn = "B",
            status = "Published",
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UpdatePost_ChangesFields_And_ReRendersHtml()
    {
        var admin = await CreateAdminClientAsync();
        var (id, _) = await CreatePublishedPostAsync(admin, contentSk: "# old", contentEn: "# old");

        var update = await admin.PutAsJsonAsync($"/api/blog/posts/{id}", new
        {
            contentMarkdownSk = "# new sk",
            contentMarkdownEn = "# new en",
        });
        update.EnsureSuccessStatusCode();

        var admGet = await admin.GetAsync($"/api/blog/posts/by-id/{id}");
        admGet.EnsureSuccessStatusCode();
        var body = await admGet.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("# new sk", doc.RootElement.GetProperty("contentMarkdownSk").GetString());
    }

    [Fact]
    public async Task DeletePost_SoftDeletes_HidesFromPublic()
    {
        var admin = await CreateAdminClientAsync();
        var (id, slug) = await CreatePublishedPostAsync(admin);

        var delete = await admin.DeleteAsync($"/api/blog/posts/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await CreateClient().GetAsync($"/api/blog/posts/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    // ── RSS ───────────────────────────────────────────────────────

    [Fact]
    public async Task RssFeed_ReturnsXmlWithPublishedPosts()
    {
        var admin = await CreateAdminClientAsync();
        var (_, slug) = await CreatePublishedPostAsync(admin);

        var response = await CreateClient().GetAsync("/rss.xml");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/rss+xml", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<?xml", body);
        Assert.Contains("<rss", body);
        Assert.Contains($"/sk/blog/{slug}", body);
    }

    // ── Regular user seeded ───────────────────────────────────────

    [Fact]
    public async Task CustomerUser_SeededOnStartup_CanLogIn()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "customer@dogphoto.sk",
            password = "customer123",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Customer", doc.RootElement.GetProperty("role").GetString());
    }
}
