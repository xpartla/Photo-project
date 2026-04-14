namespace DogPhoto.Infrastructure.Email;

public static class OrderEmailTemplates
{
    public record OrderItemInfo(string Title, int Quantity, decimal UnitPrice, int? EditionNumber);

    public static string CustomerOrderConfirmation(
        Guid orderId,
        List<OrderItemInfo> items,
        decimal totalAmount,
        string currency)
    {
        var orderRef = orderId.ToString()[..8].ToUpper();
        var itemRows = string.Join("", items.Select(i =>
        {
            var edition = i.EditionNumber.HasValue ? $" (Edition #{i.EditionNumber})" : "";
            return $"""
            <tr>
              <td style="padding: 8px 0; border-bottom: 1px solid #eee;">{i.Title}{edition}</td>
              <td style="padding: 8px 0; border-bottom: 1px solid #eee; text-align: right;">{i.UnitPrice} {currency}</td>
            </tr>
            """;
        }));

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: 'Nunito', Arial, sans-serif; background: #FFF3E0; color: #2A2A2A; padding: 32px;">
          <div style="max-width: 560px; margin: 0 auto; background: #FFF8F0; border: 3px solid #2A2A2A; border-radius: 12px; padding: 32px; box-shadow: 4px 4px 0 #2A2A2A;">
            <h1 style="font-family: 'Titan One', cursive; font-size: 1.5rem; margin: 0 0 24px;">PartlPhoto</h1>
            <h2 style="font-size: 1.25rem; margin: 0 0 16px;">Order Confirmation</h2>
            <p>Thank you for your order! Your payment has been confirmed.</p>
            <p><strong>Order #{orderRef}</strong></p>
            <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
              <tr>
                <th style="padding: 8px 0; text-align: left; border-bottom: 2px solid #2A2A2A;">Item</th>
                <th style="padding: 8px 0; text-align: right; border-bottom: 2px solid #2A2A2A;">Price</th>
              </tr>
              {itemRows}
              <tr>
                <td style="padding: 12px 0; font-weight: 700; font-size: 1.1rem;">Total</td>
                <td style="padding: 12px 0; font-weight: 700; font-size: 1.1rem; text-align: right;">{totalAmount} {currency}</td>
              </tr>
            </table>
            <p>We will prepare your prints and notify you when they ship.</p>
            <p style="margin-top: 24px; font-size: 0.85rem; color: #666;">PartlPhoto &mdash; Fine art film &amp; dog photography, Bratislava</p>
          </div>
        </body>
        </html>
        """;
    }

    public static string PhotographerOrderNotification(
        Guid orderId,
        string customerEmail,
        List<OrderItemInfo> items,
        decimal totalAmount,
        string? shippingAddressJson)
    {
        var orderRef = orderId.ToString()[..8].ToUpper();
        var itemRows = string.Join("", items.Select(i =>
        {
            var edition = i.EditionNumber.HasValue ? $" (Edition #{i.EditionNumber})" : "";
            return $"<tr><td style=\"padding: 8px 0;\">{i.Title}{edition}</td><td style=\"padding: 8px 0; text-align: right;\">{i.Quantity} &times; {i.UnitPrice} EUR</td></tr>";
        }));

        var addressSection = !string.IsNullOrEmpty(shippingAddressJson)
            ? $"<tr><td style=\"padding: 8px 0; font-weight: 700;\">Shipping</td><td style=\"padding: 8px 0;\">{shippingAddressJson}</td></tr>"
            : "";

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: 'Nunito', Arial, sans-serif; background: #FFF3E0; color: #2A2A2A; padding: 32px;">
          <div style="max-width: 560px; margin: 0 auto; background: #FFF8F0; border: 3px solid #2A2A2A; border-radius: 12px; padding: 32px; box-shadow: 4px 4px 0 #2A2A2A;">
            <h1 style="font-family: 'Titan One', cursive; font-size: 1.5rem; margin: 0 0 24px;">New Order #{orderRef}</h1>
            <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
              <tr><td style="padding: 8px 0; font-weight: 700;">Customer</td><td style="padding: 8px 0;">{customerEmail}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Total</td><td style="padding: 8px 0;">{totalAmount} EUR</td></tr>
              {addressSection}
            </table>
            <h3 style="margin: 16px 0 8px;">Items</h3>
            <table style="width: 100%; border-collapse: collapse;">
              {itemRows}
            </table>
          </div>
        </body>
        </html>
        """;
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

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: 'Nunito', Arial, sans-serif; background: #FFF3E0; color: #2A2A2A; padding: 32px;">
          <div style="max-width: 560px; margin: 0 auto; background: #FFF8F0; border: 3px solid #2A2A2A; border-radius: 12px; padding: 32px; box-shadow: 4px 4px 0 #2A2A2A;">
            <h1 style="font-family: 'Titan One', cursive; font-size: 1.5rem; margin: 0 0 24px;">PartlPhoto</h1>
            <h2 style="font-size: 1.25rem; margin: 0 0 16px;">Order Update</h2>
            <p>Hi {customerName},</p>
            <p><strong>Order #{orderRef}</strong> &mdash; {statusDisplay}.</p>
            <p style="margin-top: 24px; font-size: 0.85rem; color: #666;">PartlPhoto &mdash; Fine art film &amp; dog photography, Bratislava</p>
          </div>
        </body>
        </html>
        """;
    }
}
