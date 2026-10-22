using OrderSystem.Models;

namespace OrderSystem.Interfaces.Services
{
    public interface IPaymentGateway
    {
        Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct);
        Task RefundAsync(string transactionId, CancellationToken ct);
    }
}
