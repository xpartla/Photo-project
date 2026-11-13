using System.Net;
using DogPhoto.SharedKernel.Localization;

namespace DogPhoto.Infrastructure.Email;

/// <summary>
/// Customer e-mails are written in the language the booking was made in;
/// the photographer notification is always English.
/// </summary>
public static class BookingEmailTemplates
{
    public static string CustomerConfirmationSubject(string lang) =>
        Languages.Pick(lang, "Potvrdenie rezervácie — PartlPhoto", "Booking Confirmation — PartlPhoto");

    public static string CustomerConfirmedSubject(string lang) =>
        Languages.Pick(lang, "Vaša rezervácia je potvrdená — PartlPhoto", "Your booking is confirmed — PartlPhoto");

    public static string CustomerCancelledSubject(string lang) =>
        Languages.Pick(lang, "Vaša rezervácia bola zrušená — PartlPhoto", "Your booking was cancelled — PartlPhoto");

    /// <summary>Shown instead of a date or time when the booking has no slot yet.</summary>
    public static string ToBeArranged(string lang) => Languages.Pick(lang, "upresníme", "TBD");

    public static string CustomerConfirmation(
        string lang,
        string clientName,
        string sessionTypeName,
        string date,
        string time,
        decimal price,
        string currency)
    {
        return Layout(
            lang,
            brand: "PartlPhoto",
            title: Languages.Pick(lang, "Potvrdenie rezervácie", "Booking Confirmation"),
            body: $"""
            {Greeting(lang, clientName)}
            <p>{Languages.Pick(lang, "Ďakujeme za vašu rezerváciu! Tu sú podrobnosti:", "Thank you for your booking! Here are the details:")}</p>
            {DetailsTable(
                Row(Languages.Pick(lang, "Fotenie", "Session"), sessionTypeName),
                Row(Languages.Pick(lang, "Dátum", "Date"), date),
                Row(Languages.Pick(lang, "Čas", "Time"), time),
                Row(Languages.Pick(lang, "Cena", "Price"), $"{price} {currency}"))}
            <p>{Languages.Pick(lang,
                "Vaša rezervácia <strong>čaká na potvrdenie</strong>. Čoskoro sa vám ozveme.",
                "Your booking status is <strong>pending</strong>. We will confirm it shortly.")}</p>
            """);
    }

    public static string CustomerConfirmed(
        string lang,
        string clientName,
        string sessionTypeName,
        string date,
        string time,
        decimal price,
        string currency)
    {
        return Layout(
            lang,
            brand: "PartlPhoto",
            title: Languages.Pick(lang, "Vaša rezervácia je potvrdená!", "Your booking is confirmed!"),
            body: $"""
            {Greeting(lang, clientName)}
            <p>{Languages.Pick(lang,
                "Dobrá správa — vaša rezervácia bola potvrdená. Tu sú podrobnosti:",
                "Great news — your booking has been confirmed. Here are the details:")}</p>
            {DetailsTable(
                Row(Languages.Pick(lang, "Fotenie", "Session"), sessionTypeName),
                Row(Languages.Pick(lang, "Dátum", "Date"), date),
                Row(Languages.Pick(lang, "Čas", "Time"), time),
                Row(Languages.Pick(lang, "Cena", "Price"), $"{price} {currency}"))}
            <p>{Languages.Pick(lang,
                "Tešíme sa na vás. Ak by sa u vás niečo zmenilo, stačí odpovedať na tento e-mail.",
                "We look forward to seeing you. If anything changes on your end, just reply to this email.")}</p>
            """);
    }

    public static string CustomerCancelled(
        string lang,
        string clientName,
        string sessionTypeName,
        string date,
        string time)
    {
        return Layout(
            lang,
            brand: "PartlPhoto",
            title: Languages.Pick(lang, "Rezervácia zrušená", "Booking cancelled"),
            body: $"""
            {Greeting(lang, clientName)}
            <p>{Languages.Pick(lang,
                "Vaša rezervácia bola zrušená. Pre úplnosť uvádzame jej podrobnosti:",
                "Your booking has been cancelled. Here were the details for reference:")}</p>
            {DetailsTable(
                Row(Languages.Pick(lang, "Fotenie", "Session"), sessionTypeName),
                Row(Languages.Pick(lang, "Dátum", "Date"), date),
                Row(Languages.Pick(lang, "Čas", "Time"), time))}
            <p>{Languages.Pick(lang,
                "Ak ide o omyl alebo si chcete dohodnúť nový termín, stačí odpovedať na tento e-mail a radi vám pomôžeme.",
                "If this was a mistake or you'd like to rebook, just reply to this email and we'll help out.")}</p>
            """);
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
        return Layout(
            Languages.English,
            brand: "New Booking",
            title: null,
            body: DetailsTable(
                Row("Client", clientName),
                Row("Email", clientEmail),
                string.IsNullOrEmpty(clientPhone) ? "" : Row("Phone", clientPhone),
                Row("Session", sessionTypeName),
                Row("Date", date),
                Row("Time", time),
                Row("Dogs", dogCount.ToString()),
                string.IsNullOrEmpty(specialRequests) ? "" : Row("Special requests", specialRequests)),
            footer: false);
    }

    // ── Shared fragments ───────────────────────────────────────────────

    private static string Layout(string lang, string brand, string? title, string body, bool footer = true) => $"""
        <!DOCTYPE html>
        <html lang="{lang}">
        <head><meta charset="utf-8"></head>
        <body style="font-family: 'Nunito', Arial, sans-serif; background: #FFF3E0; color: #2A2A2A; padding: 32px;">
          <div style="max-width: 560px; margin: 0 auto; background: #FFF8F0; border: 3px solid #2A2A2A; border-radius: 12px; padding: 32px; box-shadow: 4px 4px 0 #2A2A2A;">
            <h1 style="font-family: 'Titan One', cursive; font-size: 1.5rem; margin: 0 0 24px;">{E(brand)}</h1>
            {(title is null ? "" : $"<h2 style=\"font-size: 1.25rem; margin: 0 0 16px;\">{E(title)}</h2>")}
            {body}
            {(footer ? $"<p style=\"margin-top: 24px; font-size: 0.85rem; color: #666;\">{Languages.Pick(lang, "PartlPhoto &mdash; fotografia psov, Bratislava", "PartlPhoto &mdash; Dog photography, Bratislava")}</p>" : "")}
          </div>
        </body>
        </html>
        """;

    private static string Greeting(string lang, string clientName) =>
        $"<p>{Languages.Pick(lang, "Dobrý deň", "Hi")} <strong>{E(clientName)}</strong>,</p>";

    private static string DetailsTable(params string[] rows) =>
        $"<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">{string.Concat(rows)}</table>";

    private static string Row(string label, string value) =>
        $"<tr><td style=\"padding: 8px 0; font-weight: 700;\">{E(label)}</td><td style=\"padding: 8px 0;\">{E(value)}</td></tr>";

    // Names, e-mails and special requests come straight from a public form.
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
