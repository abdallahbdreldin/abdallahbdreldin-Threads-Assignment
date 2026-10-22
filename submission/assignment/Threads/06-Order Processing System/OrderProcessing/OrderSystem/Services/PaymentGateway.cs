using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class PaymentGateway : IPaymentGateway
    {
        private readonly IJsonFileStore _jsonFileStore;
        private readonly HashSet<int> _timeoutOrderIds;
        private readonly HashSet<int> _declineOrderIds;

        public PaymentGateway(
            IJsonFileStore jsonFileStore,
            IEnumerable<int>? timeoutOrderIds = null,
            IEnumerable<int>? declineOrderIds = null)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
            _timeoutOrderIds = timeoutOrderIds?.ToHashSet() ?? new HashSet<int>();
            _declineOrderIds = declineOrderIds?.ToHashSet() ?? new HashSet<int>();
        }

        public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct)
        {
            Log.Info($"{nameof(PaymentGateway)} -> Order #{orderId} authorizing payment...");
            await Task.Delay(800, ct);

            Log.Info($"{nameof(PaymentGateway)} -> Order #{orderId} fraud check...");
            await Task.Delay(500, ct);

            if (_declineOrderIds.Contains(orderId))
            {
                var declinedTransaction = new PaymentTransaction
                {
                    OrderId = orderId,
                    Amount = amount,
                    Status = PaymentStatus.Declined,
                    CreatedAt = DateTime.Now
                };
                await AppendPaymentAsync(declinedTransaction, ct);
                Log.Error($"{nameof(PaymentGateway)} -> Order #{orderId} declined: Card declined by issuer");

                return new PaymentResult
                {
                    Success = false,
                    Status = PaymentStatus.Declined,
                    Message = "Card declined by issuer"
                };
            }

            int captureDelay = _timeoutOrderIds.Contains(orderId) ? 5000 : 700;
            Log.Info($"{nameof(PaymentGateway)} -> Order #{orderId} capturing payment...");
            await Task.Delay(captureDelay, ct);

            string transactionId = $"TXN-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
            var capturedTransaction = new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = PaymentStatus.Captured,
                TransactionId = transactionId,
                CreatedAt = DateTime.Now
            };
            await AppendPaymentAsync(capturedTransaction, ct);
            Log.Success($"{nameof(PaymentGateway)} -> Order #{orderId} payment captured ({transactionId})");

            return new PaymentResult
            {
                Success = true,
                Status = PaymentStatus.Captured,
                TransactionId = transactionId,
                Message = "Payment captured"
            };
        }

        public async Task RefundAsync(string transactionId, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

            Log.Info($"{nameof(PaymentGateway)} -> Refunding {transactionId}...");
            await Task.Delay(500, ct);

            bool refunded = false;
            await _jsonFileStore.UpdateAsync<List<PaymentTransaction>>(DataFiles.Payments, payments =>
            {
                PaymentTransaction? payment = payments.FirstOrDefault(candidate => candidate.TransactionId == transactionId);
                if (payment is not null && payment.Status == PaymentStatus.Captured)
                {
                    payment.Status = PaymentStatus.Refunded;
                    refunded = true;
                }

                return payments;
            }, ct);

            if (!refunded)
                throw new InvalidOperationException($"Captured payment {transactionId} was not found for refund.");

            Log.Success($"{nameof(PaymentGateway)} -> {transactionId} refunded");
        }

        private Task AppendPaymentAsync(PaymentTransaction payment, CancellationToken ct) =>
            _jsonFileStore.UpdateAsync<List<PaymentTransaction>>(DataFiles.Payments, payments =>
            {
                payments.Add(payment);
                return payments;
            }, ct);
    }
}
