namespace DogPhoto.Infrastructure.Email;

public static class OrderEmailTemplates
{
    public record OrderItemInfo(
        string Title,
        string? FormatName,
        string? PaperTypeName,
        string? ImageUrl,
        int Quantity,
        decimal UnitPrice,
        int? EditionNumber);

    public record OrderAddress(
        string Name,
        string Street,
        string City,
        string PostalCode,
        string Country);

    // ── Shared palette (inline-style-friendly) ─────────────────────────
    private const string C_Dark = "#2A2A2A";
    private const string C_White = "#FFF8F0";
    private const string C_Cream = "#FFF3E0";
    private const string C_Peach = "#F2B880";
    private const string C_Sage = "#7EAA92";
    private const string C_Terracotta = "#D4896A";
    private const string C_Muted = "rgba(42,42,42,0.65)";
    private const string C_Divider = "rgba(42,42,42,0.1)";

    private const string FontFamily = "'Nunito','Helvetica Neue',Arial,sans-serif";
    private const string FontHeading = "'Titan One','Nunito','Helvetica Neue',Arial,sans-serif";

    // ── Public templates ───────────────────────────────────────────────

    public static string CustomerOrderConfirmation(
        Guid orderId,
        List<OrderItemInfo> items,
        decimal totalAmount,
        string currency,
        OrderAddress? shippingAddress = null)
    {
        var orderRef = orderId.ToString()[..8].ToUpper();

        return Wrap(
            $"""
            <div style="text-align:center;margin:0 0 24px;">
              <div style="font-family:{FontHeading};font-size:26px;line-height:1.1;color:{C_Dark};margin:0 0 6px;">Order confirmed!</div>
              <div style="font-size:15px;color:{C_Muted};">Thank you &mdash; your payment has been received.</div>
            </div>

            {OrderNumberCard(orderRef)}

            <h3 style="font-family:{FontHeading};font-size:18px;margin:24px 0 8px;color:{C_Dark};">What you ordered</h3>
            {ItemsTable(items, currency)}

            {TotalRow(totalAmount, currency)}

            {(shippingAddress is not null ? AddressBlock("Shipping to", shippingAddress) : "")}

            <p style="margin:20px 0 0;font-size:14px;color:{C_Muted};">
              We'll prepare your prints and notify you when they ship.
            </p>
            """,
            heading: "PartlPhoto");
    }

    public static string PhotographerOrderNotification(
        Guid orderId,
        string customerEmail,
        List<OrderItemInfo> items,
        decimal totalAmount,
        string currency,
        OrderAddress? shippingAddress)
    {
        var orderRef = orderId.ToString()[..8].ToUpper();

        return Wrap(
            $"""
            <div style="text-align:center;margin:0 0 24px;">
              <div style="font-family:{FontHeading};font-size:24px;line-height:1.1;color:{C_Dark};margin:0 0 6px;">New order</div>
              <div style="font-family:{FontHeading};font-size:18px;color:{C_Peach};">#{orderRef}</div>
            </div>

            <table role="presentation" style="width:100%;border-collapse:collapse;margin:0 0 20px;">
              <tr>
                <td style="padding:10px 0;font-size:13px;color:{C_Muted};text-transform:uppercase;letter-spacing:0.5px;width:110px;">Customer</td>
                <td style="padding:10px 0;font-size:15px;color:{C_Dark};font-weight:700;"><a href="mailto:{E(customerEmail)}" style="color:{C_Dark};text-decoration:underline;">{E(customerEmail)}</a></td>
              </tr>
              <tr>
                <td style="padding:10px 0;font-size:13px;color:{C_Muted};text-transform:uppercase;letter-spacing:0.5px;">Total</td>
                <td style="padding:10px 0;font-family:{FontHeading};font-size:20px;color:{C_Dark};">{totalAmount} {currency}</td>
              </tr>
            </table>

            {(shippingAddress is not null ? AddressBlock("Ship to", shippingAddress) : "")}

            <h3 style="font-family:{FontHeading};font-size:18px;margin:24px 0 8px;color:{C_Dark};">Items</h3>
            {ItemsTable(items, currency)}
            """,
            heading: "PartlPhoto — Admin");
    }

    public static string OrderStatusUpdate(Guid orderId, string newStatus, string customerName)
    {
        var orderRef = orderId.ToString()[..8].ToUpper();
        var statusDisplay = newStatus switch
        {
            "processing" => "Your order is being prepared",
            "shipped" => "Your order has been shipped",
            "completed" => "Your order has been delivered",
            "cancelled" => "Your order has been cancelled",
            "refunded" => "Your order has been refunded",
            _ => $"Your order status is now: {newStatus}"
        };

        return Wrap(
            $"""
            <h2 style="font-family:{FontHeading};font-size:22px;margin:0 0 16px;color:{C_Dark};">Order update</h2>
            <p style="font-size:15px;color:{C_Dark};margin:0 0 8px;">Hi {E(customerName)},</p>
            <p style="font-size:15px;color:{C_Dark};margin:0 0 16px;"><strong>Order #{orderRef}</strong> &mdash; {E(statusDisplay)}.</p>
            """,
            heading: "PartlPhoto");
    }

    // ── Shared fragments ───────────────────────────────────────────────

    private static string Wrap(string innerHtml, string heading) => $"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
        </head>
        <body style="margin:0;padding:32px 16px;font-family:{FontFamily};background:{C_Cream};color:{C_Dark};">
          <table role="presentation" style="max-width:600px;margin:0 auto;width:100%;border-collapse:collapse;">
            <tr><td>
              <div style="background:{C_White};border:3px solid {C_Dark};border-radius:14px;padding:32px;box-shadow:4px 4px 0 {C_Dark};">
                <div style="font-family:{FontHeading};font-size:18px;color:{C_Dark};margin:0 0 24px;letter-spacing:0.5px;">{E(heading)}</div>
                {innerHtml}
              </div>
              <p style="margin:16px 0 0;text-align:center;font-size:12px;color:{C_Muted};">
                PartlPhoto &mdash; Fine art film &amp; dog photography, Bratislava
              </p>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string OrderNumberCard(string orderRef) => $"""
        <div style="text-align:center;padding:14px;border:2px solid {C_Dark};border-radius:10px;background:{C_Cream};margin:0 0 8px;">
          <div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;color:{C_Muted};font-weight:700;margin-bottom:2px;">Order number</div>
          <div style="font-family:{FontHeading};font-size:22px;color:{C_Dark};">#{orderRef}</div>
        </div>
        """;

    private static string ItemsTable(List<OrderItemInfo> items, string currency)
    {
        var rows = string.Join("", items.Select(i => ItemRow(i, currency)));
        return $"""
            <table role="presentation" style="width:100%;border-collapse:collapse;">
              {rows}
            </table>
            """;
    }

    private static string ItemRow(OrderItemInfo i, string currency)
    {
        var thumb = !string.IsNullOrEmpty(i.ImageUrl)
            ? $"""<img src="{E(i.ImageUrl)}" alt="{E(i.Title)}" width="72" height="72" style="display:block;border:2px solid {C_Dark};border-radius:6px;object-fit:cover;background:{C_Cream};" />"""
            : $"""<div style="width:72px;height:72px;border:2px solid {C_Dark};border-radius:6px;background:{C_Cream};"></div>""";

        var qty = i.Quantity > 1
            ? $" <span style=\"font-weight:400;color:{C_Muted};\">&times; {i.Quantity}</span>"
            : "";

        var pills = string.Join("",
            new[] { i.FormatName, i.PaperTypeName }
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(p => Pill(p!)));

        var edition = i.EditionNumber.HasValue
            ? $"""<div style="margin-top:6px;font-size:12px;color:{C_Terracotta};font-weight:700;text-transform:uppercase;letter-spacing:0.5px;">Edition #{i.EditionNumber}</div>"""
            : "";

        var lineTotal = i.UnitPrice * i.Quantity;

        return $"""
            <tr>
              <td width="84" style="padding:12px 0;border-bottom:1px solid {C_Divider};vertical-align:top;">{thumb}</td>
              <td style="padding:12px 12px;border-bottom:1px solid {C_Divider};vertical-align:top;">
                <div style="font-weight:700;font-size:15px;color:{C_Dark};">{E(i.Title)}{qty}</div>
                {(pills.Length > 0 ? $"<div style=\"margin-top:6px;\">{pills}</div>" : "")}
                {edition}
              </td>
              <td style="padding:12px 0;border-bottom:1px solid {C_Divider};vertical-align:top;text-align:right;white-space:nowrap;">
                <div style="font-weight:700;color:{C_Dark};font-size:15px;">{lineTotal} {E(currency)}</div>
              </td>
            </tr>
            """;
    }

    private static string Pill(string text) => $"""
        <span style="display:inline-block;font-size:12px;font-weight:700;line-height:1.2;color:{C_Dark};padding:3px 10px;border:1.5px solid {C_Dark};border-radius:999px;background:{C_Cream};margin:0 4px 2px 0;">{E(text)}</span>
        """;

    private static string TotalRow(decimal total, string currency) => $"""
        <table role="presentation" style="width:100%;border-collapse:collapse;margin:8px 0 0;">
          <tr>
            <td style="padding:14px 0 0;font-size:15px;font-weight:700;color:{C_Dark};border-top:2px solid {C_Dark};">Total</td>
            <td style="padding:14px 0 0;font-family:{FontHeading};font-size:22px;color:{C_Dark};text-align:right;border-top:2px solid {C_Dark};">{total} {E(currency)}</td>
          </tr>
        </table>
        """;

    private static string AddressBlock(string label, OrderAddress a) => $"""
        <div style="margin:24px 0 0;padding:16px 18px;border:2px solid {C_Dark};border-radius:10px;background:{C_Cream};">
          <div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;font-weight:700;color:{C_Terracotta};margin-bottom:8px;">{E(label)}</div>
          <div style="font-weight:700;font-size:15px;color:{C_Dark};margin-bottom:4px;">{E(a.Name)}</div>
          <div style="font-size:14px;color:{C_Dark};line-height:1.5;">
            {E(a.Street)}<br>
            {E(a.PostalCode)} {E(a.City)}<br>
            {E(a.Country)}
          </div>
        </div>
        """;

    // ── HTML escape helper ─────────────────────────────────────────────

    private static string E(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
    }
}
