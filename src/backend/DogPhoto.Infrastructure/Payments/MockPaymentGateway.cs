using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace DogPhoto.Infrastructure.Payments;

public class MockPaymentGateway(IConfiguration configuration) : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, MockPayment> _payments = new();
    private readonly string _frontendBaseUrl = configuration["FrontendBaseUrl"] ?? "http://localhost:4321";

    public Task<PaymentSession> CreatePaymentAsync(Guid orderId, decimal amount, string currency, string returnUrl, string cancelUrl, CancellationToken ct = default)
    {
        var paymentId = $"mock_{Guid.NewGuid():N}";

        _payments[paymentId] = new MockPayment
        {
            PaymentId = paymentId,
            OrderId = orderId,
            Amount = amount,
            Currency = currency,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            Status = PaymentState.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var redirectUrl = $"{_frontendBaseUrl}/mock-pay/{paymentId}";
        return Task.FromResult(new PaymentSession(paymentId, redirectUrl));
    }

    public Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
            return Task.FromResult(new PaymentStatusResult(PaymentState.Pending, null));

        return Task.FromResult(new PaymentStatusResult(payment.Status, payment.PaidAt));
    }

    public Task<RefundResult> RefundPaymentAsync(string paymentId, decimal amount, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
            return Task.FromResult(new RefundResult(false, null, "Payment not found"));

        if (payment.Status != PaymentState.Paid)
            return Task.FromResult(new RefundResult(false, null, "Payment is not in paid state"));

        payment.Status = PaymentState.Refunded;
        var refundId = $"refund_{Guid.NewGuid():N}";
        return Task.FromResult(new RefundResult(true, refundId, null));
    }

    /// <summary>
    /// Confirms a mock payment. Called by the webhook endpoint when the mock payment page submits.
    /// </summary>
    public bool ConfirmPayment(string paymentId)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
            return false;

        if (payment.Status != PaymentState.Pending)
            return false;

        payment.Status = PaymentState.Paid;
        payment.PaidAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Cancels a mock payment. Called by the webhook endpoint when the customer cancels.
    /// </summary>
    public bool CancelPayment(string paymentId)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
            return false;

        if (payment.Status != PaymentState.Pending)
            return false;

        payment.Status = PaymentState.Cancelled;
        return true;
    }

    /// <summary>
    /// Gets mock payment details (for the mock payment page to display amount).
    /// </summary>
    public MockPayment? GetPayment(string paymentId)
    {
        _payments.TryGetValue(paymentId, out var payment);
        return payment;
    }

    public class MockPayment
    {
        public string PaymentId { get; set; } = default!;
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string ReturnUrl { get; set; } = default!;
        public string CancelUrl { get; set; } = default!;
        public PaymentState Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
