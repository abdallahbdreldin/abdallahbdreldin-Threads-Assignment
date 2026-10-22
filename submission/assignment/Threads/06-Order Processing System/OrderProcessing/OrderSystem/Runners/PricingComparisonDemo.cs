using System.Diagnostics;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;
using OrderSystem.Services;

namespace OrderSystem.Runners
{
    public sealed class PricingComparisonDemo
    {
        private readonly IIncomingOrderRepository _incomingOrderRepository;
        private readonly OrderPricer _orderPricer;

        public PricingComparisonDemo(
            IIncomingOrderRepository incomingOrderRepository,
            OrderPricer orderPricer)
        {
            _incomingOrderRepository = incomingOrderRepository ?? throw new ArgumentNullException(nameof(incomingOrderRepository));
            _orderPricer = orderPricer ?? throw new ArgumentNullException(nameof(orderPricer));
        }

        public async Task RunAsync(CancellationToken ct)
        {
            List<Order> orders = await _incomingOrderRepository.GetAllAsync(ct);

            Order order = orders.FirstOrDefault(order => order.Id == 1)
                ?? throw new InvalidOperationException("Incoming Order #1 was not found.");

            Order sequentialOrder = CopyOrder(order);
            Order concurrentOrder = CopyOrder(order);

            var stopwatch = Stopwatch.StartNew();
            await _orderPricer.PriceOrderSequentialAsync(sequentialOrder, ct);
            stopwatch.Stop();

            TimeSpan sequentialTime = stopwatch.Elapsed;

            stopwatch.Restart();
            await _orderPricer.PriceOrderAsync(concurrentOrder, ct);
            stopwatch.Stop();

            Console.WriteLine($"Sequential pricing for Order #1: {sequentialOrder.TotalAmount:F2} in {sequentialTime.TotalSeconds:F2} seconds");
            Console.WriteLine($"Concurrent pricing for Order #1: {concurrentOrder.TotalAmount:F2} in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        }

        private static Order CopyOrder(Order source) => new()
        {
            Id = source.Id,
            CustomerId = source.CustomerId,
            Items = source.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            CouponCode = source.CouponCode,
            SubTotal = source.SubTotal,
            Discount = source.Discount,
            ShippingCost = source.ShippingCost,
            TotalAmount = source.TotalAmount,
            Status = source.Status,
            CreatedAt = source.CreatedAt,
            FailureReason = source.FailureReason,
            TransactionId = source.TransactionId
        };
    }
}
