namespace DogPhoto.Infrastructure.Payments;

public interface IPaymentGateway
{
    /// <summary>False when no gateway is configured; checkout must refuse orders instead of creating unpayable ones.</summary>
    bool IsAvailable { get; }

    Task<PaymentSession> CreatePaymentAsync(Guid orderId, decimal amount, string currency, string returnUrl, string cancelUrl, CancellationToken ct = default);
    Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);
    Task<RefundResult> RefundPaymentAsync(string paymentId, decimal amount, CancellationToken ct = default);
}
