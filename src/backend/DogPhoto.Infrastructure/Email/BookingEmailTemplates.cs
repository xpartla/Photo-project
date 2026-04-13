namespace DogPhoto.Infrastructure.Email;

public static class BookingEmailTemplates
{
    public static string CustomerConfirmation(
        string clientName,
        string sessionTypeName,
        string date,
        string time,
        decimal price,
        string currency)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: 'Nunito', Arial, sans-serif; background: #FFF3E0; color: #2A2A2A; padding: 32px;">
          <div style="max-width: 560px; margin: 0 auto; background: #FFF8F0; border: 3px solid #2A2A2A; border-radius: 12px; padding: 32px; box-shadow: 4px 4px 0 #2A2A2A;">
            <h1 style="font-family: 'Titan One', cursive; font-size: 1.5rem; margin: 0 0 24px;">PartlPhoto</h1>
            <h2 style="font-size: 1.25rem; margin: 0 0 16px;">Booking Confirmation</h2>
            <p>Hi <strong>{clientName}</strong>,</p>
            <p>Thank you for your booking! Here are the details:</p>
            <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
              <tr><td style="padding: 8px 0; font-weight: 700;">Session</td><td style="padding: 8px 0;">{sessionTypeName}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Date</td><td style="padding: 8px 0;">{date}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Time</td><td style="padding: 8px 0;">{time}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Price</td><td style="padding: 8px 0;">{price} {currency}</td></tr>
            </table>
            <p>Your booking status is <strong>pending</strong>. We will confirm it shortly.</p>
            <p style="margin-top: 24px; font-size: 0.85rem; color: #666;">PartlPhoto &mdash; Fine art film &amp; dog photography, Bratislava</p>
          </div>
        </body>
        </html>
        """;
    }

    public static string PhotographerNotification(
        string clientName,
        string clientEmail,
        string? clientPhone,
        string sessionTypeName,
        string date,
        string time,
        int dogCount,
        string? specialRequests)
    {
        var phoneRow = !string.IsNullOrEmpty(clientPhone)
            ? $"<tr><td style=\"padding: 8px 0; font-weight: 700;\">Phone</td><td style=\"padding: 8px 0;\">{clientPhone}</td></tr>"
            : "";
        var requestsRow = !string.IsNullOrEmpty(specialRequests)
            ? $"<tr><td style=\"padding: 8px 0; font-weight: 700;\">Special requests</td><td style=\"padding: 8px 0;\">{specialRequests}</td></tr>"
            : "";

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: 'Nunito', Arial, sans-serif; background: #FFF3E0; color: #2A2A2A; padding: 32px;">
          <div style="max-width: 560px; margin: 0 auto; background: #FFF8F0; border: 3px solid #2A2A2A; border-radius: 12px; padding: 32px; box-shadow: 4px 4px 0 #2A2A2A;">
            <h1 style="font-family: 'Titan One', cursive; font-size: 1.5rem; margin: 0 0 24px;">New Booking</h1>
            <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
              <tr><td style="padding: 8px 0; font-weight: 700;">Client</td><td style="padding: 8px 0;">{clientName}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Email</td><td style="padding: 8px 0;">{clientEmail}</td></tr>
              {phoneRow}
              <tr><td style="padding: 8px 0; font-weight: 700;">Session</td><td style="padding: 8px 0;">{sessionTypeName}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Date</td><td style="padding: 8px 0;">{date}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Time</td><td style="padding: 8px 0;">{time}</td></tr>
              <tr><td style="padding: 8px 0; font-weight: 700;">Dogs</td><td style="padding: 8px 0;">{dogCount}</td></tr>
              {requestsRow}
            </table>
          </div>
        </body>
        </html>
        """;
    }
}
