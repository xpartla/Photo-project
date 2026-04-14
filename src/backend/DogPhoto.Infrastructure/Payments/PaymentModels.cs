namespace DogPhoto.Infrastructure.Payments;

public record PaymentSession(string PaymentId, string RedirectUrl);

public record PaymentStatusResult(PaymentState Status, DateTime? PaidAt);

public record RefundResult(bool Success, string? RefundId, string? Error);

public enum PaymentState
{
    Pending,
    Paid,
    Cancelled,
    Refunded
}
