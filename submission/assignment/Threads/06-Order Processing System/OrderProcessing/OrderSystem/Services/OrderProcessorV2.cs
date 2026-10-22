using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class OrderProcessorV2
    {
        private readonly ReserveStock _reserveStock;
        private readonly IProductRepository _productRepository;
        private readonly OrderPricer _orderPricer;
        private readonly InvoiceGenerator _invoiceGenerator;
        private readonly IOrderRepository _orderRepository;

        public OrderProcessorV2(
            ReserveStock reserveStock,
            IProductRepository productRepository,
            OrderPricer orderPricer,
            InvoiceGenerator invoiceGenerator,
            IOrderRepository orderRepository)
        {
            _reserveStock = reserveStock ?? throw new ArgumentNullException(nameof(reserveStock));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _orderPricer = orderPricer ?? throw new ArgumentNullException(nameof(orderPricer));
            _invoiceGenerator = invoiceGenerator ?? throw new ArgumentNullException(nameof(invoiceGenerator));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        }

        public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);

            var reservedItems = new List<OrderItem>();
            foreach (OrderItem item in order.Items)
            {
                bool reserved = await _reserveStock.ReserveStockAsync(item.ProductId, item.Quantity, ct);
                if (reserved)
                {
                    reservedItems.Add(item);
                    continue;
                }

                Product? product = await _productRepository.GetByIdAsync(item.ProductId, ct);
                foreach (OrderItem reservedItem in reservedItems)
                    await _reserveStock.ReleaseStockAsync(reservedItem.ProductId, reservedItem.Quantity, ct);

                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{product?.Name ?? $"Product #{item.ProductId}"}'";
                await _orderRepository.SaveAsync(order, ct);
                return false;
            }

            await _orderPricer.PriceOrderAsync(order, ct);
            await _invoiceGenerator.GenerateAsync(order, ct);

            order.Status = OrderStatus.Completed;
            order.FailureReason = null;
            await _orderRepository.SaveAsync(order, ct);
            return true;
        }
    }
}
