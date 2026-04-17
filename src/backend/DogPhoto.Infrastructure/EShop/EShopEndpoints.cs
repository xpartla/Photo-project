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

        // ── Public: formats & paper types ──────────────────────────────

        group.MapGet("/formats", async (EShopDbContext db) =>
        {
            var formats = await db.Formats.OrderBy(f => f.DisplayOrder).ThenBy(f => f.NameEn).ToListAsync();
            return Results.Ok(formats.Select(f => new { id = f.Id, code = f.Code, nameSk = f.NameSk, nameEn = f.NameEn }));
        });

        group.MapGet("/paper-types", async (EShopDbContext db) =>
        {
            var papers = await db.PaperTypes.OrderBy(p => p.DisplayOrder).ThenBy(p => p.NameEn).ToListAsync();
            return Results.Ok(papers.Select(p => new { id = p.Id, code = p.Code, nameSk = p.NameSk, nameEn = p.NameEn }));
        });

        // ── Public: portfolio-backed collections & tags surfaced to shop ──

        group.MapGet("/collections", async (EShopDbContext db, PortfolioDbContext portfolioDb, string? lang) =>
        {
            var l = lang ?? "sk";

            // Collections are shop-visible when at least one product references a photo in them.
            var productPhotoIds = await db.Products
                .Where(p => p.PhotoId.HasValue)
                .Select(p => p.PhotoId!.Value)
                .Distinct().ToListAsync();
            if (productPhotoIds.Count == 0) return Results.Ok(Array.Empty<object>());

            var collections = await portfolioDb.Collections
                .Include(c => c.CollectionPhotos)
                .Where(c => c.CollectionPhotos.Any(cp => productPhotoIds.Contains(cp.PhotoId)))
                .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
                .ToListAsync();

            var coverPhotoIds = collections
                .Where(c => c.CoverPhotoId.HasValue).Select(c => c.CoverPhotoId!.Value)
                .Distinct().ToList();
            var coverVariants = coverPhotoIds.Count > 0
                ? await portfolioDb.PhotoVariants.Where(v => coverPhotoIds.Contains(v.PhotoId)).ToListAsync()
                : new List<PhotoVariant>();

            return Results.Ok(collections.Select(c =>
            {
                var productCount = c.CollectionPhotos.Count(cp => productPhotoIds.Contains(cp.PhotoId));
                return new
                {
                    slug = c.Slug,
                    name = l == "en" ? c.NameEn : c.NameSk,
                    nameSk = c.NameSk,
                    nameEn = c.NameEn,
                    description = l == "en" ? c.DescriptionEn ?? c.DescriptionSk : c.DescriptionSk ?? c.DescriptionEn,
                    productCount,
                    coverImage = c.CoverPhotoId.HasValue ? MapCoverImage(coverVariants, c.CoverPhotoId.Value) : null
                };
            }));
        });

        group.MapGet("/collections/{slug}", async (
            string slug, EShopDbContext db, PortfolioDbContext portfolioDb, string? lang) =>
        {
            var l = lang ?? "sk";

            var collection = await portfolioDb.Collections
                .Include(c => c.CollectionPhotos)
                .FirstOrDefaultAsync(c => c.Slug == slug);
            if (collection is null) return Results.NotFound();

            var collectionPhotoIds = collection.CollectionPhotos.Select(cp => cp.PhotoId).ToList();
            if (collectionPhotoIds.Count == 0)
                return Results.NotFound();

            var products = await db.Products
                .Include(p => p.Variants).ThenInclude(v => v.Format)
                .Include(p => p.Variants).ThenInclude(v => v.PaperType)
                .Where(p => p.PhotoId.HasValue && collectionPhotoIds.Contains(p.PhotoId.Value))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            if (products.Count == 0) return Results.NotFound();

            var photoIds = products.Select(p => p.PhotoId!.Value).Concat(
                collection.CoverPhotoId.HasValue ? new[] { collection.CoverPhotoId.Value } : Array.Empty<Guid>()
            ).Distinct().ToList();
            var photoVariants = await portfolioDb.PhotoVariants.Where(v => photoIds.Contains(v.PhotoId)).ToListAsync();
            var tagsByPhoto = await LoadTagsByPhoto(portfolioDb, photoIds);

            return Results.Ok(new
            {
                slug = collection.Slug,
                name = l == "en" ? collection.NameEn : collection.NameSk,
                nameSk = collection.NameSk,
                nameEn = collection.NameEn,
                description = l == "en" ? collection.DescriptionEn ?? collection.DescriptionSk : collection.DescriptionSk ?? collection.DescriptionEn,
                coverImage = collection.CoverPhotoId.HasValue ? MapCoverImage(photoVariants, collection.CoverPhotoId.Value) : null,
                products = products.Select(p => MapProduct(p, l, photoVariants, tagsByPhoto))
            });
        });

        group.MapGet("/tags", async (EShopDbContext db, PortfolioDbContext portfolioDb, string? lang) =>
        {
            var l = lang ?? "sk";

            var productPhotoIds = await db.Products
                .Where(p => p.PhotoId.HasValue)
                .Select(p => p.PhotoId!.Value)
                .Distinct().ToListAsync();
            if (productPhotoIds.Count == 0) return Results.Ok(Array.Empty<object>());

            var tagCounts = await portfolioDb.Photos
                .Where(p => productPhotoIds.Contains(p.Id))
                .SelectMany(p => p.PhotoTags.Select(pt => pt.Tag))
                .GroupBy(t => new { t.Slug, t.NameSk, t.NameEn })
                .Select(g => new { g.Key.Slug, g.Key.NameSk, g.Key.NameEn, Count = g.Count() })
                .ToListAsync();

            return Results.Ok(tagCounts
                .OrderByDescending(t => t.Count).ThenBy(t => t.NameEn)
                .Select(t => new
                {
                    slug = t.Slug,
                    name = l == "en" ? t.NameEn : t.NameSk,
                    nameSk = t.NameSk,
                    nameEn = t.NameEn,
                    productCount = t.Count
                }));
        });

        // ── Public: products ───────────────────────────────────────────

        group.MapGet("/products", async (
            EShopDbContext db,
            PortfolioDbContext portfolioDb,
            string? lang,
            bool? available,
            Guid? photoId,
            string? collection,
            string? tag,
            string? format,
            string? paperType,
            string? q,
            int page = 1,
            int size = 20) =>
        {
            var l = lang ?? "sk";
            var query = db.Products.Include(p => p.Variants).ThenInclude(v => v.Format)
                                    .Include(p => p.Variants).ThenInclude(v => v.PaperType)
                                    .AsQueryable();

            if (available.HasValue)
                query = query.Where(p => p.IsAvailable == available.Value);
            if (photoId.HasValue)
                query = query.Where(p => p.PhotoId == photoId.Value);
            if (!string.IsNullOrEmpty(format))
                query = query.Where(p => p.Variants.Any(v => v.Format.Code == format));
            if (!string.IsNullOrEmpty(paperType))
                query = query.Where(p => p.Variants.Any(v => v.PaperType.Code == paperType));

            // Cross-module filters (collection, tag, search across photo text) resolve
            // the matching photo IDs in Portfolio and narrow the product query by them.
            if (!string.IsNullOrEmpty(collection))
            {
                var ids = await portfolioDb.Collections
                    .Where(c => c.Slug == collection)
                    .SelectMany(c => c.CollectionPhotos.Select(cp => cp.PhotoId))
                    .ToListAsync();
                query = query.Where(p => p.PhotoId.HasValue && ids.Contains(p.PhotoId.Value));
            }
            if (!string.IsNullOrEmpty(tag))
            {
                var ids = await portfolioDb.Photos
                    .Where(p => p.PhotoTags.Any(pt => pt.Tag.Slug == tag))
                    .Select(p => p.Id).ToListAsync();
                query = query.Where(p => p.PhotoId.HasValue && ids.Contains(p.PhotoId.Value));
            }
            if (!string.IsNullOrEmpty(q))
            {
                var pattern = $"%{q}%";
                var photoHits = await portfolioDb.Photos
                    .Where(p => EF.Functions.ILike(p.TitleSk ?? "", pattern)
                             || EF.Functions.ILike(p.TitleEn ?? "", pattern)
                             || EF.Functions.ILike(p.Location ?? "", pattern))
                    .Select(p => p.Id).ToListAsync();
                query = query.Where(p =>
                    EF.Functions.ILike(p.TitleSk, pattern) ||
                    EF.Functions.ILike(p.TitleEn, pattern) ||
                    EF.Functions.ILike(p.DescriptionSk ?? "", pattern) ||
                    EF.Functions.ILike(p.DescriptionEn ?? "", pattern) ||
                    (p.PhotoId.HasValue && photoHits.Contains(p.PhotoId.Value)));
            }

            var total = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var photoIds = products.Where(p => p.PhotoId.HasValue).Select(p => p.PhotoId!.Value).Distinct().ToList();
            var photoVariants = photoIds.Count > 0
                ? await portfolioDb.PhotoVariants.Where(v => photoIds.Contains(v.PhotoId)).ToListAsync()
                : [];
            var tagsByPhoto = await LoadTagsByPhoto(portfolioDb, photoIds);

            return Results.Ok(new
            {
                items = products.Select(p => MapProduct(p, l, photoVariants, tagsByPhoto)),
                total,
                page,
                size
            });
        });

        group.MapGet("/products/{slug}", async (string slug, EShopDbContext db, PortfolioDbContext portfolioDb, string? lang) =>
        {
            var product = await db.Products
                .Include(p => p.Variants).ThenInclude(v => v.Format)
                .Include(p => p.Variants).ThenInclude(v => v.PaperType)
                .FirstOrDefaultAsync(p => p.Slug == slug);
            if (product is null) return Results.NotFound();

            var l = lang ?? "sk";
            var photoVariants = product.PhotoId.HasValue
                ? await portfolioDb.PhotoVariants.Where(v => v.PhotoId == product.PhotoId.Value).ToListAsync()
                : [];
            var photoIds = product.PhotoId.HasValue ? new List<Guid> { product.PhotoId.Value } : new();
            var tagsByPhoto = await LoadTagsByPhoto(portfolioDb, photoIds);

            return Results.Ok(MapProduct(product, l, photoVariants, tagsByPhoto));
        });

        group.MapPost("/webhooks/payment", async (
            PaymentWebhookRequest request,
            EShopDbContext db,
            PortfolioDbContext portfolioDb,
            IPaymentGateway gateway,
            IEmailService emailService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EShop.Webhook");

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

                if (gateway is MockPaymentGateway mock)
                    mock.ConfirmPayment(request.PaymentId);

                order.Status = "paid";
                order.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                try
                {
                    // Look up each order item's product photo for thumbnails.
                    var productIds = order.Items.Select(oi => oi.ProductId).Distinct().ToList();
                    var productPhotos = await db.Products.IgnoreQueryFilters()
                        .Where(p => productIds.Contains(p.Id))
                        .Select(p => new { p.Id, p.PhotoId })
                        .ToDictionaryAsync(p => p.Id, p => p.PhotoId);
                    var photoIds = productPhotos.Values
                        .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
                    var photoVariants = photoIds.Count > 0
                        ? await portfolioDb.PhotoVariants.Where(v => photoIds.Contains(v.PhotoId)).ToListAsync()
                        : new List<PhotoVariant>();

                    var items = order.Items
                        .Select(oi =>
                        {
                            productPhotos.TryGetValue(oi.ProductId, out var photoId);
                            var img = EmailImageUrl(PickThumbnail(photoVariants, photoId));
                            return new OrderEmailTemplates.OrderItemInfo(
                                oi.ProductTitleEn,
                                oi.FormatNameEn,
                                oi.PaperTypeNameEn,
                                img,
                                oi.Quantity,
                                oi.UnitPrice,
                                oi.EditionNumber);
                        })
                        .ToList();

                    var shippingAddress = ParseAddress(order.ShippingAddressJson);

                    var customerHtml = OrderEmailTemplates.CustomerOrderConfirmation(
                        order.Id, items, order.TotalAmount, order.Currency, shippingAddress);
                    await emailService.SendAsync(
                        order.CustomerEmail ?? "customer@example.com",
                        $"Order Confirmation #{order.Id.ToString()[..8]} — PartlPhoto",
                        customerHtml);

                    var photographerHtml = OrderEmailTemplates.PhotographerOrderNotification(
                        order.Id, order.CustomerEmail ?? "unknown", items, order.TotalAmount, order.Currency, shippingAddress);
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

                    await RestoreEditionCounts(db, order.Id);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { orderId = order.Id, status = order.Status });
            }

            return Results.BadRequest(new { error = $"Unknown payment status: {request.Status}" });
        });

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

        auth.MapGet("/cart", async (EShopDbContext db, PortfolioDbContext portfolioDb, ICurrentUser currentUser) =>
        {
            var cart = await db.ShoppingCarts
                .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
                .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Format)
                .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.PaperType)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null)
                return Results.Ok(new { items = Array.Empty<object>(), total = 0m });

            var photoIds = cart.Items
                .Select(ci => ci.Variant.Product.PhotoId)
                .Where(id => id.HasValue).Select(id => id!.Value)
                .Distinct().ToList();
            var photoVariants = photoIds.Count > 0
                ? await portfolioDb.PhotoVariants.Where(v => photoIds.Contains(v.PhotoId)).ToListAsync()
                : [];

            return Results.Ok(new
            {
                items = cart.Items.Select(ci => new
                {
                    id = ci.Id,
                    variantId = ci.VariantId,
                    productId = ci.Variant.ProductId,
                    productSlug = ci.Variant.Product.Slug,
                    title = ci.Variant.Product.TitleEn,
                    titleSk = ci.Variant.Product.TitleSk,
                    titleEn = ci.Variant.Product.TitleEn,
                    formatCode = ci.Variant.Format.Code,
                    formatNameSk = ci.Variant.Format.NameSk,
                    formatNameEn = ci.Variant.Format.NameEn,
                    paperTypeCode = ci.Variant.PaperType.Code,
                    paperTypeNameSk = ci.Variant.PaperType.NameSk,
                    paperTypeNameEn = ci.Variant.PaperType.NameEn,
                    price = ci.Variant.Price,
                    currency = ci.Variant.Product.Currency,
                    quantity = ci.Quantity,
                    editionSize = ci.Variant.Product.EditionSize,
                    editionSold = ci.Variant.Product.EditionSold,
                    isAvailable = ci.Variant.Product.IsAvailable && ci.Variant.IsAvailable,
                    imageUrl = PickThumbnail(photoVariants, ci.Variant.Product.PhotoId)
                }),
                total = cart.Items.Sum(ci => ci.Variant.Price * ci.Quantity)
            });
        });

        auth.MapPost("/cart/items", async (
            AddToCartRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            var variant = await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == request.VariantId);
            if (variant is null)
                return Results.NotFound(new { error = "Variant not found." });
            if (!variant.IsAvailable || !variant.Product.IsAvailable)
                return Results.BadRequest(new { error = "Variant is not available." });

            var cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null)
            {
                cart = new ShoppingCart { UserId = currentUser.UserId };
                db.ShoppingCarts.Add(cart);
            }

            var existing = cart.Items.FirstOrDefault(ci => ci.VariantId == request.VariantId);
            if (existing is not null)
                existing.Quantity += request.Quantity;
            else
                cart.Items.Add(new CartItem { VariantId = request.VariantId, Quantity = request.Quantity });

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
                db.CartItems.Remove(item);
            else
                item.Quantity = request.Quantity;

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
                var variant = await ResolveVariantAsync(db, localItem);
                if (variant is null || !variant.IsAvailable || !variant.Product.IsAvailable) continue;

                var existing = cart.Items.FirstOrDefault(ci => ci.VariantId == variant.Id);
                if (existing is not null)
                    existing.Quantity += localItem.Quantity;
                else
                    cart.Items.Add(new CartItem { VariantId = variant.Id, Quantity = localItem.Quantity });
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
                .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
                .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Format)
                .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.PaperType)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId);

            if (cart is null || cart.Items.Count == 0)
                return Results.BadRequest(new { error = "Cart is empty." });

            // Validate variant availability + edition capacity (per-product, not per-variant).
            var productLimits = cart.Items
                .GroupBy(ci => ci.Variant.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(ci => ci.Quantity));

            foreach (var item in cart.Items)
            {
                var variant = item.Variant;
                var product = variant.Product;
                if (!variant.IsAvailable || !product.IsAvailable)
                    return Results.BadRequest(new { error = $"'{product.TitleEn}' is no longer available." });
                if (product.EditionSize.HasValue &&
                    product.EditionSold + productLimits[product.Id] > product.EditionSize.Value)
                    return Results.BadRequest(new { error = $"'{product.TitleEn}' does not have enough editions available." });
            }

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
                var variant = cartItem.Variant;
                var product = variant.Product;

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
                        VariantId = variant.Id,
                        Quantity = 1,
                        UnitPrice = variant.Price,
                        EditionNumber = editionNumber,
                        ProductTitleSk = product.TitleSk,
                        ProductTitleEn = product.TitleEn,
                        FormatNameSk = variant.Format.NameSk,
                        FormatNameEn = variant.Format.NameEn,
                        PaperTypeNameSk = variant.PaperType.NameSk,
                        PaperTypeNameEn = variant.PaperType.NameEn
                    });
                }

                total += variant.Price * cartItem.Quantity;
                product.UpdatedAt = DateTime.UtcNow;
            }

            order.TotalAmount = total;
            order.Items = orderItems;

            db.Orders.Add(order);
            db.CartItems.RemoveRange(cart.Items);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogWarning("Concurrency conflict during order creation for user {UserId}", currentUser.UserId);
                return Results.Conflict(new { error = "A product edition was just purchased by another customer. Please try again." });
            }

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

        auth.MapPost("/formats", async (CreateFormatRequest request, EShopDbContext db, ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();
            if (await db.Formats.AnyAsync(f => f.Code == request.Code))
                return Results.Conflict(new { error = $"Format '{request.Code}' already exists." });

            var format = new Format
            {
                Code = request.Code,
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                DisplayOrder = request.DisplayOrder ?? 0
            };
            db.Formats.Add(format);
            await db.SaveChangesAsync();
            return Results.Created($"/api/shop/formats/{format.Id}",
                new { id = format.Id, code = format.Code, nameSk = format.NameSk, nameEn = format.NameEn });
        });

        auth.MapPost("/paper-types", async (CreatePaperTypeRequest request, EShopDbContext db, ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();
            if (await db.PaperTypes.AnyAsync(p => p.Code == request.Code))
                return Results.Conflict(new { error = $"Paper type '{request.Code}' already exists." });

            var paper = new PaperType
            {
                Code = request.Code,
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                DisplayOrder = request.DisplayOrder ?? 0
            };
            db.PaperTypes.Add(paper);
            await db.SaveChangesAsync();
            return Results.Created($"/api/shop/paper-types/{paper.Id}",
                new { id = paper.Id, code = paper.Code, nameSk = paper.NameSk, nameEn = paper.NameEn });
        });

        auth.MapPost("/products", async (
            CreateProductRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (await db.Products.AnyAsync(p => p.Slug == request.Slug))
                return Results.Conflict(new { error = $"Product with slug '{request.Slug}' already exists." });

            if (request.Variants.Count == 0)
                return Results.BadRequest(new { error = "Product must have at least one variant." });
            if (request.IsLimitedEdition && request.Variants.Count != 1)
                return Results.BadRequest(new { error = "Limited edition products must have exactly one variant." });

            var formatCodes = request.Variants.Select(v => v.FormatCode).Distinct().ToList();
            var paperCodes = request.Variants.Select(v => v.PaperTypeCode).Distinct().ToList();
            var formats = await db.Formats.Where(f => formatCodes.Contains(f.Code)).ToDictionaryAsync(f => f.Code);
            var papers = await db.PaperTypes.Where(p => paperCodes.Contains(p.Code)).ToDictionaryAsync(p => p.Code);

            foreach (var v in request.Variants)
            {
                if (!formats.ContainsKey(v.FormatCode))
                    return Results.BadRequest(new { error = $"Unknown format code '{v.FormatCode}'." });
                if (!papers.ContainsKey(v.PaperTypeCode))
                    return Results.BadRequest(new { error = $"Unknown paper type code '{v.PaperTypeCode}'." });
            }

            var product = new Product
            {
                PhotoId = request.PhotoId,
                TitleSk = request.TitleSk,
                TitleEn = request.TitleEn,
                Slug = request.Slug,
                DescriptionSk = request.DescriptionSk,
                DescriptionEn = request.DescriptionEn,
                Currency = request.Currency ?? "EUR",
                IsLimitedEdition = request.IsLimitedEdition,
                EditionSize = request.EditionSize,
                IsAvailable = request.IsAvailable ?? true
            };

            foreach (var v in request.Variants)
            {
                product.Variants.Add(new ProductVariant
                {
                    FormatId = formats[v.FormatCode].Id,
                    PaperTypeId = papers[v.PaperTypeCode].Id,
                    Price = v.Price,
                    Sku = v.Sku,
                    IsAvailable = v.IsAvailable ?? true
                });
            }

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.Created($"/api/shop/products/{product.Slug}", new
            {
                id = product.Id,
                slug = product.Slug,
                titleEn = product.TitleEn,
                isLimitedEdition = product.IsLimitedEdition,
                editionSize = product.EditionSize,
                variantCount = product.Variants.Count
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
            if (request.EditionSize.HasValue) product.EditionSize = request.EditionSize.Value;
            if (request.IsAvailable.HasValue) product.IsAvailable = request.IsAvailable.Value;
            product.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new { id = product.Id, slug = product.Slug, message = "Product updated." });
        });

        auth.MapDelete("/products/{id:guid}", async (
            Guid id,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            product.DeletedAt = DateTime.UtcNow;
            product.IsAvailable = false;
            product.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        auth.MapPost("/products/{id:guid}/variants", async (
            Guid id,
            CreateVariantRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var product = await db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id);
            if (product is null) return Results.NotFound();
            if (product.IsLimitedEdition)
                return Results.BadRequest(new { error = "Limited edition products cannot have additional variants." });

            var format = await db.Formats.FirstOrDefaultAsync(f => f.Code == request.FormatCode);
            if (format is null) return Results.BadRequest(new { error = $"Unknown format '{request.FormatCode}'." });
            var paper = await db.PaperTypes.FirstOrDefaultAsync(p => p.Code == request.PaperTypeCode);
            if (paper is null) return Results.BadRequest(new { error = $"Unknown paper type '{request.PaperTypeCode}'." });

            if (product.Variants.Any(v => v.FormatId == format.Id && v.PaperTypeId == paper.Id))
                return Results.Conflict(new { error = "Variant with this format and paper type already exists." });

            var variant = new ProductVariant
            {
                ProductId = product.Id,
                FormatId = format.Id,
                PaperTypeId = paper.Id,
                Price = request.Price,
                Sku = request.Sku,
                IsAvailable = request.IsAvailable ?? true
            };
            db.ProductVariants.Add(variant);
            await db.SaveChangesAsync();

            return Results.Created($"/api/shop/variants/{variant.Id}",
                new { id = variant.Id, price = variant.Price });
        });

        auth.MapPut("/variants/{id:guid}", async (
            Guid id,
            UpdateVariantRequest request,
            EShopDbContext db,
            ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var variant = await db.ProductVariants.FindAsync(id);
            if (variant is null) return Results.NotFound();

            if (request.Price.HasValue) variant.Price = request.Price.Value;
            if (request.Sku is not null) variant.Sku = request.Sku;
            if (request.IsAvailable.HasValue) variant.IsAvailable = request.IsAvailable.Value;
            variant.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(new { id = variant.Id, message = "Variant updated." });
        });

        auth.MapDelete("/variants/{id:guid}", async (Guid id, EShopDbContext db, ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();
            var variant = await db.ProductVariants.FindAsync(id);
            if (variant is null) return Results.NotFound();
            db.ProductVariants.Remove(variant);
            await db.SaveChangesAsync();
            return Results.NoContent();
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

            if (!ValidTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(request.Status))
                return Results.BadRequest(new { error = $"Cannot transition from '{order.Status}' to '{request.Status}'." });

            var previousStatus = order.Status;
            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            if (request.Status is "cancelled" or "refunded" && previousStatus != "pending_payment")
            {
                await RestoreEditionCounts(db, order.Id);
            }

            await db.SaveChangesAsync();

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

    private static async Task<ProductVariant?> ResolveVariantAsync(EShopDbContext db, SyncCartItem item)
    {
        if (item.VariantId.HasValue)
        {
            return await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == item.VariantId.Value);
        }

        if (string.IsNullOrEmpty(item.ProductSlug)) return null;

        var query = db.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Format)
            .Include(v => v.PaperType)
            .Where(v => v.Product.Slug == item.ProductSlug);

        if (!string.IsNullOrEmpty(item.FormatCode))
            query = query.Where(v => v.Format.Code == item.FormatCode);
        if (!string.IsNullOrEmpty(item.PaperTypeCode))
            query = query.Where(v => v.PaperType.Code == item.PaperTypeCode);

        return await query.FirstOrDefaultAsync();
    }

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

    private static object MapProduct(
        Product p,
        string lang,
        List<PhotoVariant> photoVariants,
        Dictionary<Guid, List<TagSummary>> tagsByPhoto)
    {
        var photoForProduct = photoVariants.Where(v => v.PhotoId == p.PhotoId).ToList();
        var prices = p.Variants.Select(v => v.Price).ToList();
        var photoTags = p.PhotoId.HasValue && tagsByPhoto.TryGetValue(p.PhotoId.Value, out var t)
            ? t
            : new List<TagSummary>();

        return new
        {
            id = p.Id,
            slug = p.Slug,
            photoId = p.PhotoId,
            title = lang == "en" ? p.TitleEn : p.TitleSk,
            titleSk = p.TitleSk,
            titleEn = p.TitleEn,
            description = lang == "en" ? p.DescriptionEn ?? p.DescriptionSk : p.DescriptionSk ?? p.DescriptionEn,
            currency = p.Currency,
            isLimitedEdition = p.IsLimitedEdition,
            editionSize = p.EditionSize,
            editionSold = p.EditionSold,
            editionRemaining = p.EditionSize.HasValue ? p.EditionSize.Value - p.EditionSold : (int?)null,
            isAvailable = p.IsAvailable,
            minPrice = prices.Count > 0 ? prices.Min() : (decimal?)null,
            maxPrice = prices.Count > 0 ? prices.Max() : (decimal?)null,
            tags = photoTags.Select(t => new
            {
                slug = t.Slug,
                name = lang == "en" ? t.NameEn : t.NameSk,
                nameSk = t.NameSk,
                nameEn = t.NameEn
            }),
            productVariants = p.Variants
                .OrderBy(v => v.Format.DisplayOrder).ThenBy(v => v.PaperType.DisplayOrder)
                .Select(v => new
                {
                    id = v.Id,
                    formatCode = v.Format.Code,
                    formatName = lang == "en" ? v.Format.NameEn : v.Format.NameSk,
                    formatNameSk = v.Format.NameSk,
                    formatNameEn = v.Format.NameEn,
                    paperTypeCode = v.PaperType.Code,
                    paperTypeName = lang == "en" ? v.PaperType.NameEn : v.PaperType.NameSk,
                    paperTypeNameSk = v.PaperType.NameSk,
                    paperTypeNameEn = v.PaperType.NameEn,
                    price = v.Price,
                    isAvailable = v.IsAvailable
                }),
            variants = photoForProduct.Select(v => new
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

    private record TagSummary(string Slug, string NameSk, string NameEn);

    private static object MapCoverImage(List<PhotoVariant> allVariants, Guid photoId)
    {
        var variants = allVariants.Where(v => v.PhotoId == photoId)
            .Select(v => new
            {
                width = v.Width,
                height = v.Height,
                format = v.Format,
                quality = v.Quality,
                blobUrl = v.BlobUrl,
                sizeBytes = v.SizeBytes
            }).ToList();
        return new { photoId, variants };
    }

    private static async Task<Dictionary<Guid, List<TagSummary>>> LoadTagsByPhoto(
        PortfolioDbContext portfolioDb, List<Guid> photoIds)
    {
        if (photoIds.Count == 0) return new();
        var rows = await portfolioDb.Photos
            .Where(p => photoIds.Contains(p.Id))
            .SelectMany(p => p.PhotoTags.Select(pt => new
            {
                PhotoId = p.Id,
                pt.Tag.Slug,
                pt.Tag.NameSk,
                pt.Tag.NameEn
            }))
            .ToListAsync();
        return rows
            .GroupBy(r => r.PhotoId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new TagSummary(r.Slug, r.NameSk, r.NameEn)).ToList());
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
                    variantId = oi.VariantId,
                    productSlug = product?.Slug,
                    productTitle = oi.ProductTitleEn,
                    productTitleSk = oi.ProductTitleSk,
                    productTitleEn = oi.ProductTitleEn,
                    formatNameSk = oi.FormatNameSk,
                    formatNameEn = oi.FormatNameEn,
                    paperTypeNameSk = oi.PaperTypeNameSk,
                    paperTypeNameEn = oi.PaperTypeNameEn,
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice,
                    editionNumber = oi.EditionNumber
                };
            }),
            createdAt = order.CreatedAt,
            updatedAt = order.UpdatedAt
        };
    }

    private static OrderEmailTemplates.OrderAddress? ParseAddress(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<AddressDto>(json);
            if (dto is null) return null;
            return new OrderEmailTemplates.OrderAddress(dto.Name, dto.Street, dto.City, dto.PostalCode, dto.Country);
        }
        catch { return null; }
    }

    // Images are stored under the internal Docker hostname; rewrite so the
    // recipient's mail client can fetch them. In production, swap the target
    // for the CDN origin.
    private static string? EmailImageUrl(string? url)
        => url?.Replace("http://azurite:10000/devstoreaccount1", "http://localhost:10000/devstoreaccount1");

    private static string? PickThumbnail(List<PhotoVariant> variants, Guid? photoId)
    {
        if (!photoId.HasValue) return null;
        var forPhoto = variants.Where(v => v.PhotoId == photoId.Value).ToList();
        if (forPhoto.Count == 0) return null;
        // Prefer the smallest JPEG — JPEG is the broadest-compatible format for a
        // quick <img src> render, and the smallest variant is enough for a thumbnail.
        var jpeg = forPhoto.Where(v => v.Format == "jpeg" || v.Format == "jpg")
            .OrderBy(v => v.Width).FirstOrDefault();
        return (jpeg ?? forPhoto.OrderBy(v => v.Width).First()).BlobUrl;
    }

}

// ── Request DTOs ───────────────────────────────────────────────────

public record AddToCartRequest(Guid VariantId, int Quantity = 1);

public record UpdateCartItemRequest(int Quantity);

public record SyncCartRequest(List<SyncCartItem> Items);
public record SyncCartItem(
    string? ProductSlug = null,
    string? FormatCode = null,
    string? PaperTypeCode = null,
    Guid? VariantId = null,
    int Quantity = 1);

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

public record CreateFormatRequest(string Code, string NameSk, string NameEn, int? DisplayOrder = null);
public record CreatePaperTypeRequest(string Code, string NameSk, string NameEn, int? DisplayOrder = null);

public record CreateProductRequest(
    string TitleSk,
    string TitleEn,
    string Slug,
    List<ProductVariantInput> Variants,
    Guid? PhotoId = null,
    string? DescriptionSk = null,
    string? DescriptionEn = null,
    string? Currency = null,
    bool IsLimitedEdition = false,
    int? EditionSize = null,
    bool? IsAvailable = null);

public record ProductVariantInput(
    string FormatCode,
    string PaperTypeCode,
    decimal Price,
    string? Sku = null,
    bool? IsAvailable = null);

public record UpdateProductRequest(
    string? TitleSk = null,
    string? TitleEn = null,
    string? DescriptionSk = null,
    string? DescriptionEn = null,
    int? EditionSize = null,
    bool? IsAvailable = null);

public record CreateVariantRequest(
    string FormatCode,
    string PaperTypeCode,
    decimal Price,
    string? Sku = null,
    bool? IsAvailable = null);

public record UpdateVariantRequest(
    decimal? Price = null,
    string? Sku = null,
    bool? IsAvailable = null);

public record PaymentWebhookRequest(string PaymentId, string Status);
