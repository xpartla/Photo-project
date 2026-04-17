using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DogPhoto.Infrastructure.Persistence.Portfolio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class EShopTests : ApiTestBase
{
    public EShopTests(ApiFactory factory) : base(factory) { }

    /// <summary>
    /// Seeds a portfolio photo (with the given tags and collection) so EShop
    /// cross-module filters have something to resolve against. Returns the
    /// photo id so tests can create products referencing it.
    /// </summary>
    private async Task<Guid> SeedPhotoWithTagsAndCollectionAsync(
        string photoSlug, string[] tagSlugs, string? collectionSlug = null, string? location = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();

        var photo = new Photo
        {
            Slug = photoSlug,
            TitleSk = $"Photo {photoSlug} SK",
            TitleEn = $"Photo {photoSlug} EN",
            Location = location,
            IsPublished = true
        };
        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        foreach (var ts in tagSlugs)
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Slug == ts);
            if (tag is null)
            {
                tag = new Tag { Slug = ts, NameSk = ts, NameEn = ts };
                db.Tags.Add(tag);
                await db.SaveChangesAsync();
            }
            db.Set<PhotoTag>().Add(new PhotoTag { PhotoId = photo.Id, TagId = tag.Id });
        }

        if (!string.IsNullOrEmpty(collectionSlug))
        {
            var col = await db.Collections.FirstOrDefaultAsync(c => c.Slug == collectionSlug);
            if (col is null)
            {
                col = new Collection
                {
                    Slug = collectionSlug,
                    NameSk = $"{collectionSlug} SK",
                    NameEn = $"{collectionSlug} EN",
                    CoverPhotoId = photo.Id
                };
                db.Collections.Add(col);
                await db.SaveChangesAsync();
            }
            db.Set<CollectionPhoto>().Add(new CollectionPhoto { CollectionId = col.Id, PhotoId = photo.Id });
        }

        await db.SaveChangesAsync();
        return photo.Id;
    }

    /// <summary>
    /// Admin creates a product with one or more variants and returns the product id,
    /// slug and the created variant ids (in the same order as the variants argument).
    /// </summary>
    private async Task<(string Slug, string Id, List<string> VariantIds)> CreateTestProductAsync(
        HttpClient admin,
        string slug,
        int? editionSize = 10,
        bool isLimitedEdition = true,
        object[]? variants = null,
        Guid? photoId = null)
    {
        variants ??= new object[]
        {
            new { formatCode = "30x40", paperTypeCode = "fine-art-310", price = 50m }
        };

        var response = await admin.PostAsJsonAsync("/api/shop/products", new
        {
            titleSk = $"Test {slug} SK",
            titleEn = $"Test {slug} EN",
            slug,
            descriptionSk = "Popis SK",
            descriptionEn = "Description EN",
            photoId,
            isLimitedEdition,
            editionSize,
            isAvailable = true,
            variants
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var detail = await admin.GetAsync($"/api/shop/products/{slug}");
        var detailBody = await detail.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(detailBody);
        var id = doc.RootElement.GetProperty("id").GetString()!;
        var variantIds = doc.RootElement.GetProperty("productVariants")
            .EnumerateArray()
            .Select(v => v.GetProperty("id").GetString()!)
            .ToList();
        return (slug, id, variantIds);
    }

    // ── Products ──────────────────────────────────────────────────

    [Fact]
    public async Task AdminCreateProduct_Returns201()
    {
        var admin = await CreateAdminClientAsync();
        var (_, id, variantIds) = await CreateTestProductAsync(admin, $"test-product-{Guid.NewGuid():N}"[..30]);
        Assert.NotNull(id);
        Assert.Single(variantIds);
    }

    [Fact]
    public async Task CreateProductDuplicateSlug_Returns409()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"dup-slug-{Guid.NewGuid():N}"[..25];
        await CreateTestProductAsync(admin, slug);
        var response = await admin.PostAsJsonAsync("/api/shop/products", new
        {
            titleSk = "Dup SK",
            titleEn = "Dup EN",
            slug,
            variants = new[] { new { formatCode = "a4", paperTypeCode = "matte-200", price = 10m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_LimitedEditionWithMultipleVariants_Returns400()
    {
        var admin = await CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync("/api/shop/products", new
        {
            titleSk = "Bad",
            titleEn = "Bad",
            slug = $"bad-le-{Guid.NewGuid():N}"[..25],
            isLimitedEdition = true,
            editionSize = 10,
            variants = new[]
            {
                new { formatCode = "a4", paperTypeCode = "matte-200", price = 10m },
                new { formatCode = "a3", paperTypeCode = "matte-200", price = 20m },
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_ReturnsProductList()
    {
        var admin = await CreateAdminClientAsync();
        await CreateTestProductAsync(admin, $"list-product-{Guid.NewGuid():N}"[..30]);

        var client = CreateClient();
        var response = await client.GetAsync("/api/shop/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetProductBySlug_ReturnsVariants()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"detail-prod-{Guid.NewGuid():N}"[..25];
        await CreateTestProductAsync(admin, slug, isLimitedEdition: false, editionSize: null, variants: new object[]
        {
            new { formatCode = "a4", paperTypeCode = "matte-200", price = 30m },
            new { formatCode = "a3", paperTypeCode = "matte-200", price = 50m },
        });

        var client = CreateClient();
        var response = await client.GetAsync($"/api/shop/products/{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(slug, doc.RootElement.GetProperty("slug").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("productVariants").GetArrayLength());
        Assert.Equal(30m, doc.RootElement.GetProperty("minPrice").GetDecimal());
    }

    [Fact]
    public async Task GetProductBySlug_BilingualContent()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"bilingual-{Guid.NewGuid():N}"[..25];
        await CreateTestProductAsync(admin, slug);

        var client = CreateClient();

        var skResponse = await client.GetAsync($"/api/shop/products/{slug}?lang=sk");
        var skBody = await skResponse.Content.ReadAsStringAsync();
        using var skDoc = JsonDocument.Parse(skBody);
        Assert.Contains("SK", skDoc.RootElement.GetProperty("title").GetString()!);

        var enResponse = await client.GetAsync($"/api/shop/products/{slug}?lang=en");
        var enBody = await enResponse.Content.ReadAsStringAsync();
        using var enDoc = JsonDocument.Parse(enBody);
        Assert.Contains("EN", enDoc.RootElement.GetProperty("title").GetString()!);
    }

    [Fact]
    public async Task CreateProduct_RequiresAdmin()
    {
        var customer = await CreateCustomerClientAsync();
        var response = await customer.PostAsJsonAsync("/api/shop/products", new
        {
            titleSk = "Test SK",
            titleEn = "Test EN",
            slug = "unauth-product",
            variants = new[] { new { formatCode = "a4", paperTypeCode = "matte-200", price = 10m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Formats_PaperTypes_ArePubliclyReadable()
    {
        var client = CreateClient();
        var fResp = await client.GetAsync("/api/shop/formats");
        Assert.Equal(HttpStatusCode.OK, fResp.StatusCode);
        var pResp = await client.GetAsync("/api/shop/paper-types");
        Assert.Equal(HttpStatusCode.OK, pResp.StatusCode);
    }

    // ── Cart ──────────────────────────────────────────────────────

    [Fact]
    public async Task Cart_RequiresAuth()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/shop/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CartAddAndGet_WorksCorrectly()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"cart-prod-{Guid.NewGuid():N}"[..25];
        var (_, _, variantIds) = await CreateTestProductAsync(admin, slug);

        var customer = await CreateCustomerClientAsync();

        var addResponse = await customer.PostAsJsonAsync("/api/shop/cart/items", new
        {
            variantId = variantIds[0],
            quantity = 2
        });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var cartResponse = await customer.GetAsync("/api/shop/cart");
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);
        var body = await cartResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("items").GetArrayLength() >= 1);
        Assert.True(doc.RootElement.GetProperty("total").GetDecimal() > 0);
    }

    [Fact]
    public async Task Cart_DifferentVariantsAreDistinctLines()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"multi-var-{Guid.NewGuid():N}"[..25];
        var (_, _, variantIds) = await CreateTestProductAsync(admin, slug, isLimitedEdition: false, editionSize: null, variants: new object[]
        {
            new { formatCode = "a4", paperTypeCode = "matte-200", price = 30m },
            new { formatCode = "a3", paperTypeCode = "matte-200", price = 50m },
        });

        var customer = await CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/shop/cart/items", new { variantId = variantIds[0], quantity = 1 });
        await customer.PostAsJsonAsync("/api/shop/cart/items", new { variantId = variantIds[1], quantity = 1 });

        var cart = await customer.GetAsync("/api/shop/cart");
        var body = await cart.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(2, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(80m, doc.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task CartSync_ResolvesByProductSlugAndVariantCodes()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"sync-prod-{Guid.NewGuid():N}"[..25];
        await CreateTestProductAsync(admin, slug, isLimitedEdition: false, editionSize: null, variants: new object[]
        {
            new { formatCode = "a4", paperTypeCode = "matte-200", price = 30m },
        });

        var customer = await CreateCustomerClientAsync();
        var syncResponse = await customer.PostAsJsonAsync("/api/shop/cart/sync", new
        {
            items = new[]
            {
                new { productSlug = slug, formatCode = "a4", paperTypeCode = "matte-200", quantity = 3 }
            }
        });
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        var cartResponse = await customer.GetAsync("/api/shop/cart");
        var body = await cartResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(90m, doc.RootElement.GetProperty("total").GetDecimal());
    }

    // ── Orders ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_FullCheckoutFlow()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"order-prod-{Guid.NewGuid():N}"[..25];
        var (_, _, variantIds) = await CreateTestProductAsync(admin, slug, editionSize: 10, isLimitedEdition: true, variants: new object[]
        {
            new { formatCode = "30x40", paperTypeCode = "fine-art-310", price = 75m }
        });

        var customer = await CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/shop/cart/items", new { variantId = variantIds[0], quantity = 1 });

        var orderResponse = await customer.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new
            {
                name = "Test Customer",
                street = "Test Street 1",
                city = "Bratislava",
                postalCode = "81101",
                country = "SK"
            }
        });
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);

        var orderBody = await orderResponse.Content.ReadAsStringAsync();
        using var orderDoc = JsonDocument.Parse(orderBody);
        var orderId = orderDoc.RootElement.GetProperty("orderId").GetString()!;
        var paymentId = orderDoc.RootElement.GetProperty("paymentId").GetString()!;
        var redirectUrl = orderDoc.RootElement.GetProperty("redirectUrl").GetString()!;
        Assert.NotNull(redirectUrl);
        Assert.Contains("mock-pay", redirectUrl);

        FakeEmail.Clear();
        var webhookResponse = await CreateClient().PostAsJsonAsync("/api/shop/webhooks/payment", new
        {
            paymentId,
            status = "paid"
        });
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var detailResponse = await customer.GetAsync($"/api/shop/orders/{orderId}");
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        using var detailDoc = JsonDocument.Parse(detailBody);
        Assert.Equal("paid", detailDoc.RootElement.GetProperty("status").GetString());

        // Snapshot fields are present on the order item
        var firstItem = detailDoc.RootElement.GetProperty("items").EnumerateArray().First();
        Assert.Equal("30×40 cm", firstItem.GetProperty("formatNameEn").GetString());
        Assert.Equal("Fine Art 310g (Hahnemühle)", firstItem.GetProperty("paperTypeNameEn").GetString());

        Assert.True(FakeEmail.SentEmails.Count >= 1, "At least one email should be sent on order confirmation");
    }

    [Fact]
    public async Task CreateOrder_EmptyCart_ReturnsBadRequest()
    {
        var customer = await CreateCustomerClientAsync();
        var response = await customer.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "X", street = "X", city = "X", postalCode = "X", country = "SK" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrderStatus_InvalidTransition_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"status-prod-{Guid.NewGuid():N}"[..25];
        var (_, _, variantIds) = await CreateTestProductAsync(admin, slug, editionSize: null, isLimitedEdition: false, variants: new object[]
        {
            new { formatCode = "a4", paperTypeCode = "matte-200", price = 20m }
        });

        var customer = await CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/shop/cart/items", new { variantId = variantIds[0], quantity = 1 });
        var orderResponse = await customer.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "X", street = "X", city = "X", postalCode = "X", country = "SK" }
        });
        var orderBody = await orderResponse.Content.ReadAsStringAsync();
        using var orderDoc = JsonDocument.Parse(orderBody);
        var orderId = orderDoc.RootElement.GetProperty("orderId").GetString()!;

        var invalidResponse = await admin.PutAsJsonAsync($"/api/shop/orders/{orderId}/status", new
        {
            status = "shipped"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task EditionNumber_AssignedSequentially()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"ed-seq-{Guid.NewGuid():N}"[..25];
        var (_, _, variantIds) = await CreateTestProductAsync(admin, slug, editionSize: 10, isLimitedEdition: true);

        var customer1 = await CreateCustomerClientAsync();
        await customer1.PostAsJsonAsync("/api/shop/cart/items", new { variantId = variantIds[0], quantity = 1 });
        var order1Response = await customer1.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "C1", street = "S", city = "C", postalCode = "P", country = "SK" }
        });
        order1Response.EnsureSuccessStatusCode();
        var order1Body = await order1Response.Content.ReadAsStringAsync();
        using var order1Doc = JsonDocument.Parse(order1Body);
        var order1Id = order1Doc.RootElement.GetProperty("orderId").GetString()!;

        var customer2 = await CreateCustomerClientAsync();
        await customer2.PostAsJsonAsync("/api/shop/cart/items", new { variantId = variantIds[0], quantity = 1 });
        var order2Response = await customer2.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "C2", street = "S", city = "C", postalCode = "P", country = "SK" }
        });
        order2Response.EnsureSuccessStatusCode();
        var order2Body = await order2Response.Content.ReadAsStringAsync();
        using var order2Doc = JsonDocument.Parse(order2Body);
        var order2Id = order2Doc.RootElement.GetProperty("orderId").GetString()!;

        var detail1 = await customer1.GetAsync($"/api/shop/orders/{order1Id}");
        var detail1Body = await detail1.Content.ReadAsStringAsync();
        using var d1 = JsonDocument.Parse(detail1Body);
        var ed1 = d1.RootElement.GetProperty("items").EnumerateArray().First().GetProperty("editionNumber").GetInt32();

        var detail2 = await customer2.GetAsync($"/api/shop/orders/{order2Id}");
        var detail2Body = await detail2.Content.ReadAsStringAsync();
        using var d2 = JsonDocument.Parse(detail2Body);
        var ed2 = d2.RootElement.GetProperty("items").EnumerateArray().First().GetProperty("editionNumber").GetInt32();

        Assert.Equal(1, ed1);
        Assert.Equal(2, ed2);
    }

    [Fact]
    public async Task Orders_RequiresAuth()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/shop/my-orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Filters & search ───────────────────────────────────────────

    [Fact]
    public async Task GetProducts_FilterByFormat()
    {
        var admin = await CreateAdminClientAsync();
        var slugA = $"fmt-a4-{Guid.NewGuid():N}"[..20];
        var slugB = $"fmt-a3-{Guid.NewGuid():N}"[..20];
        await CreateTestProductAsync(admin, slugA, isLimitedEdition: true, editionSize: 10,
            variants: new object[] { new { formatCode = "a4", paperTypeCode = "matte-200", price = 30m } });
        await CreateTestProductAsync(admin, slugB, isLimitedEdition: true, editionSize: 10,
            variants: new object[] { new { formatCode = "a3", paperTypeCode = "matte-200", price = 50m } });

        var client = CreateClient();
        var response = await client.GetAsync("/api/shop/products?format=a4");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var slugs = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(slugA, slugs);
        Assert.DoesNotContain(slugB, slugs);
    }

    [Fact]
    public async Task GetProducts_FilterByCollectionAndTag()
    {
        var admin = await CreateAdminClientAsync();
        var photoSlug = $"photo-{Guid.NewGuid():N}"[..20];
        var collectionSlug = $"col-{Guid.NewGuid():N}"[..20];
        var tagSlug = $"tg{Guid.NewGuid():N}"[..10];
        var photoId = await SeedPhotoWithTagsAndCollectionAsync(photoSlug, new[] { tagSlug }, collectionSlug);

        var linked = $"linked-{Guid.NewGuid():N}"[..20];
        var orphan = $"orphan-{Guid.NewGuid():N}"[..20];
        await CreateTestProductAsync(admin, linked, photoId: photoId);
        await CreateTestProductAsync(admin, orphan); // no photoId

        var client = CreateClient();

        var byCollection = await client.GetAsync($"/api/shop/products?collection={collectionSlug}");
        using var d1 = JsonDocument.Parse(await byCollection.Content.ReadAsStringAsync());
        var slugsByCol = d1.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(linked, slugsByCol);
        Assert.DoesNotContain(orphan, slugsByCol);

        var byTag = await client.GetAsync($"/api/shop/products?tag={tagSlug}");
        using var d2 = JsonDocument.Parse(await byTag.Content.ReadAsStringAsync());
        var slugsByTag = d2.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(linked, slugsByTag);
        Assert.DoesNotContain(orphan, slugsByTag);

        // Product detail should expose the inherited tag
        var detailResp = await client.GetAsync($"/api/shop/products/{linked}");
        using var dd = JsonDocument.Parse(await detailResp.Content.ReadAsStringAsync());
        var tagSlugs = dd.RootElement.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(tagSlug, tagSlugs);
    }

    [Fact]
    public async Task GetProducts_SearchMatchesTitleAndPhotoLocation()
    {
        var admin = await CreateAdminClientAsync();
        var photoSlug = $"search-photo-{Guid.NewGuid():N}"[..24];
        var rare = $"xyzzy{Guid.NewGuid():N}"[..16].ToLower();
        var photoId = await SeedPhotoWithTagsAndCollectionAsync(photoSlug, Array.Empty<string>(), location: rare);

        var match = $"search-linked-{Guid.NewGuid():N}"[..24];
        await CreateTestProductAsync(admin, match, photoId: photoId);

        var client = CreateClient();
        var resp = await client.GetAsync($"/api/shop/products?q={rare}");
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var slugs = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(match, slugs);
    }

    // ── Collections & tags endpoints ───────────────────────────────

    [Fact]
    public async Task GetCollections_IncludesOnlyCollectionsWithProducts()
    {
        var admin = await CreateAdminClientAsync();
        var photoSlug = $"c-photo-{Guid.NewGuid():N}"[..20];
        var populated = $"c-pop-{Guid.NewGuid():N}"[..20];
        var empty = $"c-empty-{Guid.NewGuid():N}"[..20];
        var photoId = await SeedPhotoWithTagsAndCollectionAsync(photoSlug, Array.Empty<string>(), populated);
        await SeedPhotoWithTagsAndCollectionAsync($"lonely-{Guid.NewGuid():N}"[..20], Array.Empty<string>(), empty);

        await CreateTestProductAsync(admin, $"cp-{Guid.NewGuid():N}"[..20], photoId: photoId);

        var client = CreateClient();
        var resp = await client.GetAsync("/api/shop/collections");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var slugs = doc.RootElement.EnumerateArray()
            .Select(c => c.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(populated, slugs);
        Assert.DoesNotContain(empty, slugs);
    }

    [Fact]
    public async Task GetCollectionBySlug_ReturnsProducts()
    {
        var admin = await CreateAdminClientAsync();
        var photoSlug = $"cd-photo-{Guid.NewGuid():N}"[..20];
        var colSlug = $"cd-col-{Guid.NewGuid():N}"[..20];
        var photoId = await SeedPhotoWithTagsAndCollectionAsync(photoSlug, Array.Empty<string>(), colSlug);
        var productSlug = $"cd-prod-{Guid.NewGuid():N}"[..20];
        await CreateTestProductAsync(admin, productSlug, photoId: photoId);

        var client = CreateClient();
        var resp = await client.GetAsync($"/api/shop/collections/{colSlug}");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(colSlug, doc.RootElement.GetProperty("slug").GetString());
        var products = doc.RootElement.GetProperty("products").EnumerateArray()
            .Select(p => p.GetProperty("slug").GetString()!).ToList();
        Assert.Contains(productSlug, products);
    }

    [Fact]
    public async Task GetTags_ReturnsTagsWithProductCounts()
    {
        var admin = await CreateAdminClientAsync();
        var tagSlug = $"tt{Guid.NewGuid():N}"[..12];
        var photoId = await SeedPhotoWithTagsAndCollectionAsync($"tg-photo-{Guid.NewGuid():N}"[..22], new[] { tagSlug });
        await CreateTestProductAsync(admin, $"tg-prod-{Guid.NewGuid():N}"[..22], photoId: photoId);

        var client = CreateClient();
        var resp = await client.GetAsync("/api/shop/tags");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var tag = doc.RootElement.EnumerateArray()
            .FirstOrDefault(t => t.GetProperty("slug").GetString() == tagSlug);
        Assert.NotEqual(JsonValueKind.Undefined, tag.ValueKind);
        Assert.True(tag.GetProperty("productCount").GetInt32() >= 1);
    }
}
