namespace DogPhoto.Infrastructure.Payments;

/// <summary>
/// Registered outside Development until a real gateway exists, so the mock
/// (which marks orders paid on an unauthenticated request) can never ship.
/// </summary>
public class UnavailablePaymentGateway : IPaymentGateway
{
    private const string Message = "No payment gateway is configured.";

    public bool IsAvailable => false;

    public Task<PaymentSession> CreatePaymentAsync(Guid orderId, decimal amount, string currency, string returnUrl, string cancelUrl, CancellationToken ct = default)
        => throw new InvalidOperationException(Message);

    public Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default)
        => throw new InvalidOperationException(Message);

    public Task<RefundResult> RefundPaymentAsync(string paymentId, decimal amount, CancellationToken ct = default)
        => throw new InvalidOperationException(Message);
}
