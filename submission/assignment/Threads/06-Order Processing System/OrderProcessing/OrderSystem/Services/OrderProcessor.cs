using System.Diagnostics;
using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class OrderProcessor
    {
        private readonly CustomerService _customerService;
        private readonly InventoryService _inventoryService;
        private readonly PricingService _pricingService;
        private readonly IPaymentGateway _paymentGateway;
        private readonly IOrderRepository _orderRepository;
        private readonly IEmailService _emailService;

        public OrderProcessor(
            CustomerService customerService,
            InventoryService inventoryService,
            PricingService pricingService,
            IPaymentGateway paymentGateway,
            IOrderRepository orderRepository,
            IEmailService emailService)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
            _paymentGateway = paymentGateway ?? throw new ArgumentNullException(nameof(paymentGateway));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);

            bool stockReserved = false;
            bool paymentCaptured = false;
            string? transactionId = null;
            Customer? customer = null;

            try
            {
                var step = Stopwatch.StartNew();
                try
                {
                    customer = await _customerService.GetCustomerAsync(order.CustomerId, ct);
                }
                catch (CustomerNotFoundException exception)
                {
                    step.Stop();
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = exception.Message;
                    await _orderRepository.SaveAsync(order, ct);
                    LogStep(order, 1, "Customer", $"REJECTED ({exception.Message})", step, false);
                    return false;
                }

                if (!customer.IsActive)
                {
                    step.Stop();
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = "Customer is inactive";
                    await _orderRepository.SaveAsync(order, ct);
                    LogStep(order, 1, "Customer", $"REJECTED ({order.FailureReason})", step, false);
                    return false;
                }

                LogStep(order, 1, "Customer", "OK", step, true);

                step.Restart();
                if (!await _inventoryService.ReserveStockAsync(order, ct))
                {
                    step.Stop();
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason ??= "Insufficient stock";
                    await _orderRepository.SaveAsync(order, ct);
                    LogStep(order, 2, "Inventory", $"REJECTED ({order.FailureReason})", step, false);
                    return false;
                }

                stockReserved = true;
                LogStep(order, 2, "Inventory", "OK", step, true);

                step.Restart();
                try
                {
                    await _pricingService.CalculateTotalAsync(order, ct);
                    LogStep(order, 3, "Pricing", "OK", step, true);
                }
                catch (InvalidCouponException exception)
                {
                    step.Stop();
                    await _inventoryService.ReleaseStockAsync(order, ct);
                    stockReserved = false;
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = exception.Message;
                    await _orderRepository.SaveAsync(order, ct);
                    LogStep(order, 3, "Pricing", $"REJECTED ({exception.Message}), released", step, false);
                    return false;
                }

                step.Restart();
                PaymentResult paymentResult;
                using (var paymentTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    paymentTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                    try
                    {
                        paymentResult = await _paymentGateway.ProcessPaymentAsync(
                            order.Id,
                            order.TotalAmount,
                            paymentTimeout.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested && paymentTimeout.IsCancellationRequested)
                    {
                        step.Stop();
                        await _inventoryService.ReleaseStockAsync(order, ct);
                        stockReserved = false;
                        order.Status = OrderStatus.PaymentFailed;
                        order.FailureReason = "Payment gateway timeout (3s).";
                        await _orderRepository.SaveAsync(order, ct);
                        LogStep(order, 4, "Payment", $"TIMEOUT after {step.ElapsedMilliseconds} ms, released", step, false);
                        return false;
                    }
                }

                if (!paymentResult.Success)
                {
                    step.Stop();
                    await _inventoryService.ReleaseStockAsync(order, ct);
                    stockReserved = false;
                    order.Status = OrderStatus.PaymentFailed;
                    order.FailureReason = paymentResult.Message ?? "Payment declined";
                    await _orderRepository.SaveAsync(order, ct);
                    LogStep(order, 4, "Payment", $"DECLINED ({order.FailureReason}), released", step, false);
                    return false;
                }

                transactionId = paymentResult.TransactionId
                    ?? throw new InvalidOperationException("The gateway captured payment without a transaction ID.");
                paymentCaptured = true;
                order.TransactionId = transactionId;
                LogStep(order, 4, "Payment", $"OK ({transactionId})", step, true);

                step.Restart();
                order.Status = OrderStatus.Completed;
                order.FailureReason = null;
                try
                {
                    await _orderRepository.SaveAsync(order, ct);
                }
                catch (DatabaseWriteException)
                {
                    await _paymentGateway.RefundAsync(transactionId, CancellationToken.None);
                    paymentCaptured = false;
                    await _inventoryService.ReleaseStockAsync(order, CancellationToken.None);
                    stockReserved = false;

                    order.Status = OrderStatus.Failed;
                    order.FailureReason = $"Database save failed after payment; payment {transactionId} refunded";
                    await _orderRepository.SaveAsync(order, CancellationToken.None);
                    LogStep(order, 5, "Save completed order", $"FAILED; {order.FailureReason}", step, false);
                    return false;
                }

                LogStep(order, 5, "Save completed order", "OK", step, true);

                step.Restart();
                try
                {
                    await _emailService.SendConfirmationAsync(order, customer, ct);
                    LogStep(order, 6, "Confirmation email", "OK", step, true);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // An email failure must not reverse a completed order.
                    LogStep(order, 6, "Confirmation email", $"FAILED ({exception.Message}); order remains Completed", step, false);
                }

                return order.Status == OrderStatus.Completed;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await CompensateAsync(order, stockReserved, paymentCaptured, transactionId);
                order.Status = OrderStatus.Pending;
                order.FailureReason = "Cancelled";
                await _orderRepository.SaveAsync(order, CancellationToken.None);
                Log.Error($"Order #{order.Id} -> Cancelled; stock released and any captured payment refunded");
                throw;
            }
            catch (Exception exception)
            {
                await CompensateAsync(order, stockReserved, paymentCaptured, transactionId);
                order.Status = OrderStatus.Failed;
                order.FailureReason = exception.Message;
                await _orderRepository.SaveAsync(order, CancellationToken.None);
                Log.Error($"Order #{order.Id} -> Failed: {exception.Message}");
                return false;
            }
        }

        private async Task CompensateAsync(Order order, bool stockReserved, bool paymentCaptured, string? transactionId)
        {
            if (paymentCaptured && transactionId is not null)
                await _paymentGateway.RefundAsync(transactionId, CancellationToken.None);

            if (stockReserved)
                await _inventoryService.ReleaseStockAsync(order, CancellationToken.None);
        }

        private static void LogStep(Order order, int stepNumber, string stepName, string result, Stopwatch stopwatch, bool success)
        {
            string message = $"Order #{order.Id} -> Step {stepNumber}/6 {stepName} ... {result} in {stopwatch.ElapsedMilliseconds} ms";
            if (success)
                Log.Success(message);
            else
                Log.Error(message);
        }
    }
}
