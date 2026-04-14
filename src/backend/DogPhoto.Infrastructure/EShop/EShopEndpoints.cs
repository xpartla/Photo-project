using System.Text.Json;
using DogPhoto.Infrastructure.Email;
using DogPhoto.Infrastructure.Payments;
using DogPhoto.Infrastructure.Persistence.EShop;
using DogPhoto.Infrastructure.Persistence.Portfolio;
using DogPhoto.SharedKernel.Auth;
using DogPhoto.SharedKernel.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DogPhoto.Infrastructure.EShop;

public static class EShopEndpoints
{
    // ── Order status state machine ────────────────────────────────────
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        ["pending_payment"] = ["paid", "cancelled"],
        ["paid"] = ["processing", "cancelled", "refunded"],
        ["processing"] = ["shipped"],
        ["shipped"] = ["completed"],
    };

    public static void MapEShopEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shop").WithTags("EShop");

        // ── Public endpoints ───────────────────────────────────────────

        group.MapGet("/products", async (
            EShopDbContext db,
            PortfolioDbContext portfolioDb,
            string? lang,
            string? format,
            bool? available,
            Guid? photoId,
            int page = 1,
            int size = 20) =>
        {
            var l = lang ?? "sk";
            var query = db.Products.AsQueryable();

            if (!string.IsNullOrEmpty(format))
                query = query.Where(p => p.Format == format);
            if (available.HasValue)
                query = query.Where(p => p.IsAvailable == available.Value);
            if (photoId.HasValue)
                query = query.Where(p => p.PhotoId == photoId.Value);

            var total = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            // Fetch photo variants for product images
            var photoIds = products.Where(p => p.PhotoId.HasValue).Select(p => p.PhotoId!.Value).Distinct().ToList();
            var photoVariants = photoIds.Count > 0
                ? await portfolioDb.PhotoVariants
                    .Where(v => photoIds.Contains(v.PhotoId))
                    .ToListAsync()
                : [];

            return Results.Ok(new
            {
                items = products.Select(p => MapProduct(p, l, photoVariants)),
                total,
                page,
                size
            });
        });

        group.MapGet("/products/{slug}", async (string slug, EShopDbContext db, PortfolioDbContext portfolioDb, string? lang) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == slug);
            if (product is null) return Results.NotFound();

            var l = lang ?? "sk";
            var variants = product.PhotoId.HasValue
                ? await portfolioDb.PhotoVariants.Where(v => v.PhotoId == product.PhotoId.Value).ToListAsync()
                : [];

            return Results.Ok(MapProduct(product, l, variants));
        });

        group.MapPost("/webhooks/payment", async (
            PaymentWebhookRequest request,
            EShopDbContext db,
            IPaymentGateway gateway,
            IEmailService emailService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EShop.Webhook");

            // Find order by payment ID
            var order = await db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.GoPayPaymentId == request.PaymentId);

            if (order is null)
            {
                logger.LogWarning("Webhook received for unknown payment {PaymentId}", request.PaymentId);
                return Results.NotFound(new { error = "Order not found for this payment." });
            }

            if (request.Status == "paid")
            {
                if (order.Status != "pending_payment")
                    return Results.Ok(new { orderId = order.Id, status = order.Status, message = "Already processed." });

                // Confirm payment in gateway
                if (gateway is MockPaymentGateway mock)
                    mock.ConfirmPayment(request.PaymentId);

                order.Status = "paid";
                order.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                // Send confirmation emails
                try
                {
                    var items = await db.OrderItems
                        .Where(oi => oi.OrderId == order.Id)
                        .Join(db.Products.IgnoreQueryFilters(), oi => oi.ProductId, p => p.Id,
                            (oi, p) => new OrderEmailTemplates.OrderItemInfo(p.TitleEn, oi.Quantity, oi.UnitPrice, oi.EditionNumber))
                        .ToListAsync();

                    var customerHtml = OrderEmailTemplates.CustomerOrderConfirmation(
                        order.Id, items, order.TotalAmount, order.Currency);
                    await emailService.SendAsync(
                        order.CustomerEmail ?? "customer@example.com",
                        $"Order Confirmation #{order.Id.ToString()[..8]} — PartlPhoto",
                        customerHtml);

                    var photographerHtml = OrderEmailTemplates.PhotographerOrderNotification(
                        order.Id, order.CustomerEmail ?? "unknown", items, order.TotalAmount, order.ShippingAddressJson);
                    await emailService.SendAsync(
                        "admin@partlphoto.sk",
                        $"New Order #{order.Id.ToString()[..8]} — {order.TotalAmount} {order.Currency}",
                        photographerHtml);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send order emails for order {OrderId}", order.Id);
                }

                return Results.Ok(new { orderId = order.Id, status = order.Status });
            }

            if (request.Status == "cancelled")
            {
                if (gateway is MockPaymentGateway mock)
                    mock.CancelPayment(request.PaymentId);

                if (order.Status == "pending_payment")
                {
                    order.Status = "cancelled";
                    order.UpdatedAt = DateTime.UtcNow;

                    // Restore edition counts
                    await RestoreEditionCounts(db, order.Id);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { orderId = order.Id, status = order.Status });
            }

            return Results.BadRequest(new { error = $"Unknown payment status: {request.Status}" });
        });

        // Payment details for mock payment page
        group.MapGet("/payments/{paymentId}", (string paymentId, IPaymentGateway gateway) =>
        {
            if (gateway is not MockPaymentGateway mock)
                return Results.NotFound();

            var payment = mock.GetPayment(paymentId);
            if (payment is null) return Results.NotFound();

            return Results.Ok(new
            {
                paymentId = payment.PaymentId,
                orderId = payment.OrderId,
                amount = payment.Amount,
                currency = payment.Currency,
                status = payment.Status.ToString().ToLower(),
                returnUrl = payment.ReturnUrl,
                cancelUrl = payment.CancelUrl
            });
        });

        // ── Authenticated endpoints ────────────────────────────────────

        var auth = app.MapGroup("/api/shop").WithTags("EShop").RequireAuthorization();

        // Cart
        auth.MapGet("/cart", async (EShopDbContext db, ICurrentUser currentUser) =>
        {
            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null)
                return Results.Ok(new { items = Array.Empty<object>(), total = 0m });

            var productIds = cart.Items.Select(ci => ci.ProductId).Distinct().ToList();
            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            return Results.Ok(new
            {
                items = cart.Items.Select(ci =>
                {
                    products.TryGetValue(ci.ProductId, out var product);
                    return new
                    {
                        id = ci.Id,
                        productId = ci.ProductId,
                        productSlug = product?.Slug,
                        title = product?.TitleEn ?? "Unknown",
                        price = product?.Price ?? 0m,
                        quantity = ci.Quantity,
                        editionSize = product?.EditionSize,
                        editionSold = product?.EditionSold ?? 0,
                        isAvailable = product?.IsAvailable ?? false
                    };
                }),
                total = cart.Items.Sum(ci =>
                {
                    products.TryGetValue(ci.ProductId, out var product);
                    return (product?.Price ?? 0m) * ci.Quantity;
                })
            });
        });

        auth.MapPost("/cart/items", async (
            AddToCartRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            var product = await db.Products.FindAsync(request.ProductId);
            if (product is null)
                return Results.NotFound(new { error = "Product not found." });
            if (!product.IsAvailable)
                return Results.BadRequest(new { error = "Product is not available." });

            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null)
            {
                cart = new ShoppingCart { UserId = currentUser.UserId };
                db.ShoppingCarts.Add(cart);
            }

            var existing = cart.Items.FirstOrDefault(ci => ci.ProductId == request.ProductId);
            if (existing is not null)
            {
                existing.Quantity += request.Quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = request.ProductId,
                    Quantity = request.Quantity
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Item added to cart." });
        });

        auth.MapPut("/cart/items/{id:guid}", async (
            Guid id,
            UpdateCartItemRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null) return Results.NotFound(new { error = "Cart not found." });

            var item = cart.Items.FirstOrDefault(ci => ci.Id == id);
            if (item is null) return Results.NotFound(new { error = "Cart item not found." });

            if (request.Quantity <= 0)
            {
                db.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = request.Quantity;
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Cart updated." });
        });

        auth.MapDelete("/cart/items/{id:guid}", async (
            Guid id,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null) return Results.NotFound(new { error = "Cart not found." });

            var item = cart.Items.FirstOrDefault(ci => ci.Id == id);
            if (item is null) return Results.NotFound(new { error = "Cart item not found." });

            db.CartItems.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Item removed from cart." });
        });

        auth.MapPost("/cart/sync", async (
            SyncCartRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null)
            {
                cart = new ShoppingCart { UserId = currentUser.UserId };
                db.ShoppingCarts.Add(cart);
            }

            foreach (var localItem in request.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == localItem.ProductSlug);
                if (product is null || !product.IsAvailable) continue;

                var existing = cart.Items.FirstOrDefault(ci => ci.ProductId == product.Id);
                if (existing is not null)
                {
                    existing.Quantity += localItem.Quantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ProductId = product.Id,
                        Quantity = localItem.Quantity
                    });
                }
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Cart synced.", itemCount = cart.Items.Count });
        });

        // Orders
        auth.MapPost("/orders", async (
            CreateOrderRequest request,
            EShopDbContext db,
            ICurrentUser currentUser,
            IPaymentGateway gateway,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EShop.Orders");

            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null || cart.Items.Count == 0)
                return Results.BadRequest(new { error = "Cart is empty." });

            // Validate all products and check edition availability
            var productIds = cart.Items.Select(ci => ci.ProductId).Distinct().ToList();
            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var item in cart.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    return Results.BadRequest(new { error = $"Product not found." });
                if (!product.IsAvailable)
                    return Results.BadRequest(new { error = $"Product '{product.TitleEn}' is no longer available." });
                if (product.EditionSize.HasValue && product.EditionSold + item.Quantity > product.EditionSize.Value)
                    return Results.BadRequest(new { error = $"Product '{product.TitleEn}' does not have enough editions available." });
            }

            // Create order
            var order = new Order
            {
                UserId = currentUser.UserId,
                Status = "pending_payment",
                Currency = "EUR",
                CustomerEmail = request.Email ?? currentUser.Email,
                ShippingAddressJson = JsonSerializer.Serialize(request.ShippingAddress),
                BillingAddressJson = request.BillingAddress is not null
                    ? JsonSerializer.Serialize(request.BillingAddress)
                    : JsonSerializer.Serialize(request.ShippingAddress)
            };

            decimal total = 0;
            var orderItems = new List<OrderItem>();

            foreach (var cartItem in cart.Items)
            {
                var product = products[cartItem.ProductId];

                for (var i = 0; i < cartItem.Quantity; i++)
                {
                    int? editionNumber = null;
                    if (product.EditionSize.HasValue)
                    {
                        product.EditionSold++;
                        editionNumber = product.EditionSold;

                        if (product.EditionSold >= product.EditionSize.Value)
                            product.IsAvailable = false;
                    }

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPrice = product.Price,
                        EditionNumber = editionNumber
                    });
                }

                total += product.Price * cartItem.Quantity;
                product.UpdatedAt = DateTime.UtcNow;
            }

            order.TotalAmount = total;
            order.Items = orderItems;

            db.Orders.Add(order);

            // Clear the cart
            db.CartItems.RemoveRange(cart.Items);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Edition was purchased concurrently — reload and check
                logger.LogWarning("Concurrency conflict during order creation for user {UserId}", currentUser.UserId);
                return Results.Conflict(new { error = "A product edition was just purchased by another customer. Please try again." });
            }

            // Create payment
            var returnUrl = request.ReturnUrl ?? $"/en/shop/orders/{order.Id}";
            var cancelUrl = request.CancelUrl ?? "/en/shop/cart";

            var payment = await gateway.CreatePaymentAsync(
                order.Id, total, order.Currency, returnUrl, cancelUrl);

            order.GoPayPaymentId = payment.PaymentId;
            await db.SaveChangesAsync();

            return Results.Created($"/api/shop/orders/{order.Id}", new
            {
                orderId = order.Id,
                paymentId = payment.PaymentId,
                redirectUrl = payment.RedirectUrl,
                total = order.TotalAmount,
                currency = order.Currency
            });
        });

        auth.MapGet("/orders/{id:guid}", async (Guid id, EShopDbContext db, ICurrentUser currentUser) =>
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null) return Results.NotFound();
            if (!currentUser.IsAdmin && order.UserId != currentUser.UserId)
                return Results.Forbid();

            var productIds = order.Items.Select(oi => oi.ProductId).Distinct().ToList();
            var products = await db.Products
                .IgnoreQueryFilters()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            return Results.Ok(MapOrder(order, products));
        });

        auth.MapGet("/my-orders", async (EShopDbContext db, ICurrentUser currentUser) =>
        {
            var orders = await db.Orders
                .Where(o => o.UserId == currentUser.UserId)
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var productIds = orders.SelectMany(o => o.Items).Select(oi => oi.ProductId).Distinct().ToList();
            var products = productIds.Count > 0
                ? await db.Products.IgnoreQueryFilters().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
                : new Dictionary<Guid, Product>();

            return Results.Ok(orders.Select(o => MapOrder(o, products)));
        });

        // ── Admin endpoints ────────────────────────────────────────────

        auth.MapPost("/products", async (
            CreateProductRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            // Check slug uniqueness
            if (await db.Products.AnyAsync(p => p.Slug == request.Slug))
                return Results.Conflict(new { error = $"Product with slug '{request.Slug}' already exists." });

            var product = new Product
            {
                PhotoId = request.PhotoId,
                TitleSk = request.TitleSk,
                TitleEn = request.TitleEn,
                Slug = request.Slug,
                DescriptionSk = request.DescriptionSk,
                DescriptionEn = request.DescriptionEn,
                Format = request.Format,
                PaperType = request.PaperType,
                Price = request.Price,
                Currency = request.Currency ?? "EUR",
                EditionSize = request.EditionSize,
                IsAvailable = request.IsAvailable ?? true
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.Created($"/api/shop/products/{product.Slug}", new
            {
                id = product.Id,
                slug = product.Slug,
                titleEn = product.TitleEn,
                price = product.Price,
                editionSize = product.EditionSize
            });
        });

        auth.MapPut("/products/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            if (request.TitleSk is not null) product.TitleSk = request.TitleSk;
            if (request.TitleEn is not null) product.TitleEn = request.TitleEn;
            if (request.DescriptionSk is not null) product.DescriptionSk = request.DescriptionSk;
            if (request.DescriptionEn is not null) product.DescriptionEn = request.DescriptionEn;
            if (request.Format is not null) product.Format = request.Format;
            if (request.PaperType is not null) product.PaperType = request.PaperType;
            if (request.Price.HasValue) product.Price = request.Price.Value;
            if (request.EditionSize.HasValue) product.EditionSize = request.EditionSize.Value;
            if (request.IsAvailable.HasValue) product.IsAvailable = request.IsAvailable.Value;
            product.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new { id = product.Id, slug = product.Slug, message = "Product updated." });
        });

        auth.MapPut("/orders/{id:guid}/status", async (
            Guid id,
            UpdateOrderStatusRequest request,
            EShopDbContext db,
            ICurrentUser currentUser,
            IEmailService emailService,
            ILoggerFactory loggerFactory) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var order = await db.Orders.FindAsync(id);
            if (order is null) return Results.NotFound();

            // Validate state transition
            if (!ValidTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(request.Status))
                return Results.BadRequest(new { error = $"Cannot transition from '{order.Status}' to '{request.Status}'." });

            var previousStatus = order.Status;
            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            // If cancelling a paid order, restore editions
            if (request.Status is "cancelled" or "refunded" && previousStatus != "pending_payment")
            {
                await RestoreEditionCounts(db, order.Id);
            }

            await db.SaveChangesAsync();

            // Send status update email
            if (order.CustomerEmail is not null)
            {
                try
                {
                    var html = OrderEmailTemplates.OrderStatusUpdate(order.Id, request.Status, order.CustomerEmail);
                    await emailService.SendAsync(
                        order.CustomerEmail,
                        $"Order #{order.Id.ToString()[..8]} — Status Update",
                        html);
                }
                catch (Exception ex)
                {
                    loggerFactory.CreateLogger("EShop").LogError(ex, "Failed to send status update email for order {OrderId}", order.Id);
                }
            }

            return Results.Ok(new { orderId = order.Id, previousStatus, newStatus = order.Status });
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static async Task RestoreEditionCounts(EShopDbContext db, Guid orderId)
    {
        var orderItems = await db.OrderItems
            .Where(oi => oi.OrderId == orderId && oi.EditionNumber.HasValue)
            .ToListAsync();

        var productIds = orderItems.Select(oi => oi.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in orderItems)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                product.EditionSold = Math.Max(0, product.EditionSold - 1);
                if (product.EditionSize.HasValue && product.EditionSold < product.EditionSize.Value)
                    product.IsAvailable = true;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private static object MapProduct(Product p, string lang, List<PhotoVariant> variants)
    {
        var photoVariants = variants.Where(v => v.PhotoId == p.PhotoId).ToList();

        return new
        {
            id = p.Id,
            slug = p.Slug,
            photoId = p.PhotoId,
            title = lang == "en" ? p.TitleEn : p.TitleSk,
            titleSk = p.TitleSk,
            titleEn = p.TitleEn,
            description = lang == "en" ? p.DescriptionEn ?? p.DescriptionSk : p.DescriptionSk ?? p.DescriptionEn,
            format = p.Format,
            paperType = p.PaperType,
            price = p.Price,
            currency = p.Currency,
            editionSize = p.EditionSize,
            editionSold = p.EditionSold,
            editionRemaining = p.EditionSize.HasValue ? p.EditionSize.Value - p.EditionSold : (int?)null,
            isAvailable = p.IsAvailable,
            variants = photoVariants.Select(v => new
            {
                width = v.Width,
                height = v.Height,
                format = v.Format,
                quality = v.Quality,
                blobUrl = v.BlobUrl,
                sizeBytes = v.SizeBytes
            })
        };
    }

    private static object MapOrder(Order order, Dictionary<Guid, Product> products)
    {
        return new
        {
            id = order.Id,
            status = order.Status,
            paymentId = order.GoPayPaymentId,
            totalAmount = order.TotalAmount,
            currency = order.Currency,
            shippingAddress = order.ShippingAddressJson,
            billingAddress = order.BillingAddressJson,
            items = order.Items.Select(oi =>
            {
                products.TryGetValue(oi.ProductId, out var product);
                return new
                {
                    id = oi.Id,
                    productId = oi.ProductId,
                    productSlug = product?.Slug,
                    productTitle = product?.TitleEn ?? "Unknown",
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice,
                    editionNumber = oi.EditionNumber
                };
            }),
            createdAt = order.CreatedAt,
            updatedAt = order.UpdatedAt
        };
    }
}

// ── Request DTOs ───────────────────────────────────────────────────

public record AddToCartRequest(Guid ProductId, int Quantity = 1);

public record UpdateCartItemRequest(int Quantity);

public record SyncCartRequest(List<SyncCartItem> Items);
public record SyncCartItem(string ProductSlug, int Quantity = 1);

public record CreateOrderRequest(
    AddressDto ShippingAddress,
    AddressDto? BillingAddress = null,
    string? Email = null,
    string? ReturnUrl = null,
    string? CancelUrl = null);

public record AddressDto(
    string Name,
    string Street,
    string City,
    string PostalCode,
    string Country = "SK");

public record UpdateOrderStatusRequest(string Status);

public record CreateProductRequest(
    string TitleSk,
    string TitleEn,
    string Slug,
    Guid? PhotoId = null,
    string? DescriptionSk = null,
    string? DescriptionEn = null,
    string? Format = null,
    string? PaperType = null,
    decimal Price = 0,
    string? Currency = null,
    int? EditionSize = null,
    bool? IsAvailable = null);

public record UpdateProductRequest(
    string? TitleSk = null,
    string? TitleEn = null,
    string? DescriptionSk = null,
    string? DescriptionEn = null,
    string? Format = null,
    string? PaperType = null,
    decimal? Price = null,
    int? EditionSize = null,
    bool? IsAvailable = null);

public record PaymentWebhookRequest(string PaymentId, string Status);
