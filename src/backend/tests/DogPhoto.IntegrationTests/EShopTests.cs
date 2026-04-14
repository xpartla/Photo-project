using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class EShopTests : ApiTestBase
{
    public EShopTests(ApiFactory factory) : base(factory) { }

    /// <summary>
    /// Helper: admin creates a product and returns its slug + ID.
    /// </summary>
    private async Task<(string Slug, string Id)> CreateTestProductAsync(
        HttpClient admin,
        string slug,
        int? editionSize = 10,
        decimal price = 50)
    {
        var response = await admin.PostAsJsonAsync("/api/shop/products", new
        {
            titleSk = $"Test {slug} SK",
            titleEn = $"Test {slug} EN",
            slug,
            descriptionSk = "Popis SK",
            descriptionEn = "Description EN",
            format = "30x40 cm",
            paperType = "Fine Art",
            price,
            editionSize,
            isAvailable = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return (slug, doc.RootElement.GetProperty("id").GetString()!);
    }

    // ── Products ──────────────────────────────────────────────────

    [Fact]
    public async Task AdminCreateProduct_Returns201()
    {
        var admin = await CreateAdminClientAsync();
        var (slug, id) = await CreateTestProductAsync(admin, $"test-product-{Guid.NewGuid():N}"[..30]);
        Assert.NotNull(id);
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
            price = 10m
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
    public async Task GetProductBySlug_ReturnsDetail()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"detail-prod-{Guid.NewGuid():N}"[..25];
        await CreateTestProductAsync(admin, slug);

        var client = CreateClient();
        var response = await client.GetAsync($"/api/shop/products/{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(slug, doc.RootElement.GetProperty("slug").GetString());
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
            price = 10m
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        var (_, productId) = await CreateTestProductAsync(admin, slug);

        var customer = await CreateCustomerClientAsync();

        // Add to cart
        var addResponse = await customer.PostAsJsonAsync("/api/shop/cart/items", new
        {
            productId,
            quantity = 2
        });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        // Get cart
        var cartResponse = await customer.GetAsync("/api/shop/cart");
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);
        var body = await cartResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("items").GetArrayLength() >= 1);
        Assert.True(doc.RootElement.GetProperty("total").GetDecimal() > 0);
    }

    [Fact]
    public async Task CartSync_MergesLocalItems()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"sync-prod-{Guid.NewGuid():N}"[..25];
        await CreateTestProductAsync(admin, slug, price: 30);

        var customer = await CreateCustomerClientAsync();
        var syncResponse = await customer.PostAsJsonAsync("/api/shop/cart/sync", new
        {
            items = new[] { new { productSlug = slug, quantity = 3 } }
        });
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        var cartResponse = await customer.GetAsync("/api/shop/cart");
        var body = await cartResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("total").GetDecimal() >= 90);
    }

    // ── Orders ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_FullCheckoutFlow()
    {
        var admin = await CreateAdminClientAsync();
        var slug = $"order-prod-{Guid.NewGuid():N}"[..25];
        var (_, productId) = await CreateTestProductAsync(admin, slug, editionSize: 10, price: 75);

        var customer = await CreateCustomerClientAsync();

        // Add to cart
        await customer.PostAsJsonAsync("/api/shop/cart/items", new { productId, quantity = 1 });

        // Create order
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

        // Simulate webhook (payment confirmed)
        FakeEmail.Clear();
        var webhookResponse = await CreateClient().PostAsJsonAsync("/api/shop/webhooks/payment", new
        {
            paymentId,
            status = "paid"
        });
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        // Verify order is paid
        var detailResponse = await customer.GetAsync($"/api/shop/orders/{orderId}");
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        using var detailDoc = JsonDocument.Parse(detailBody);
        Assert.Equal("paid", detailDoc.RootElement.GetProperty("status").GetString());

        // Verify email was sent
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
        var (_, productId) = await CreateTestProductAsync(admin, slug, editionSize: null, price: 20);

        var customer = await CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/shop/cart/items", new { productId, quantity = 1 });
        var orderResponse = await customer.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "X", street = "X", city = "X", postalCode = "X", country = "SK" }
        });
        var orderBody = await orderResponse.Content.ReadAsStringAsync();
        using var orderDoc = JsonDocument.Parse(orderBody);
        var orderId = orderDoc.RootElement.GetProperty("orderId").GetString()!;

        // Try invalid transition: pending_payment → shipped (should fail)
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
        var (_, productId) = await CreateTestProductAsync(admin, slug, editionSize: 10, price: 30);

        // Two customers buy the same product
        var customer1 = await CreateCustomerClientAsync();
        await customer1.PostAsJsonAsync("/api/shop/cart/items", new { productId, quantity = 1 });
        var order1Response = await customer1.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "C1", street = "S", city = "C", postalCode = "P", country = "SK" }
        });
        order1Response.EnsureSuccessStatusCode();
        var order1Body = await order1Response.Content.ReadAsStringAsync();
        using var order1Doc = JsonDocument.Parse(order1Body);
        var order1Id = order1Doc.RootElement.GetProperty("orderId").GetString()!;

        var customer2 = await CreateCustomerClientAsync();
        await customer2.PostAsJsonAsync("/api/shop/cart/items", new { productId, quantity = 1 });
        var order2Response = await customer2.PostAsJsonAsync("/api/shop/orders", new
        {
            shippingAddress = new { name = "C2", street = "S", city = "C", postalCode = "P", country = "SK" }
        });
        order2Response.EnsureSuccessStatusCode();
        var order2Body = await order2Response.Content.ReadAsStringAsync();
        using var order2Doc = JsonDocument.Parse(order2Body);
        var order2Id = order2Doc.RootElement.GetProperty("orderId").GetString()!;

        // Check edition numbers
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
}
