using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;
using OrderSystem.Services;

namespace OrderSystem.Runners
{
    internal sealed class RaceConditionDemo
    {
        private const int ProductId = 4;
        private const int FirstOrderId = 10;
        private const int SecondOrderId = 11;

        private readonly IJsonFileStore _jsonFileStore;
        private readonly IOrderRepository _orderRepository;
        private readonly ReserveStock _reserveStock;

        public RaceConditionDemo(IJsonFileStore jsonFileStore, IOrderRepository orderRepository)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _reserveStock = new ReserveStock(_jsonFileStore);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var orders = await _orderRepository.GetAllAsync(ct);
            var order10 = orders.FirstOrDefault(order => order.Id == FirstOrderId)
                ?? throw new InvalidOperationException($"Order #{FirstOrderId} not found.");
            var order11 = orders.FirstOrDefault(order => order.Id == SecondOrderId)
                ?? throw new InvalidOperationException($"Order #{SecondOrderId} not found.");

            int quantity10 = GetProductQuantity(order10);
            int quantity11 = GetProductQuantity(order11);
            if (quantity10 <= 0 || quantity11 <= 0)
                throw new InvalidOperationException($"Orders #{FirstOrderId} and #{SecondOrderId} must each contain Product #{ProductId}.");

            Console.WriteLine("Unsafe reservations:");
            await SetProductStockAsync(1, ct);

            var unsafeTask10 = _reserveStock.ReserveStockUnsafeAsync(ProductId, quantity10, ct);
            var unsafeTask11 = _reserveStock.ReserveStockUnsafeAsync(ProductId, quantity11, ct);
            await Task.WhenAll(unsafeTask10, unsafeTask11);

            Console.WriteLine($"Order #{FirstOrderId} unsafe reservation completed for {quantity10} unit(s).");
            Console.WriteLine($"Order #{SecondOrderId} unsafe reservation completed for {quantity11} unit(s).");
            Console.WriteLine($"Stock after unsafe reservations: {await ReadProductStockAsync(ct)}");

            Console.WriteLine("\nSafe reservations:");
            await SetProductStockAsync(1, ct);

            var safeTask10 = _reserveStock.ReserveStockAsync(ProductId, quantity10, ct);
            var safeTask11 = _reserveStock.ReserveStockAsync(ProductId, quantity11, ct);
            var reservationResults = await Task.WhenAll(safeTask10, safeTask11);

            Console.WriteLine($"Order #{FirstOrderId} safe reservation succeeded: {reservationResults[0]}");
            Console.WriteLine($"Order #{SecondOrderId} safe reservation succeeded: {reservationResults[1]}");
            Console.WriteLine($"Stock after safe reservations: {await ReadProductStockAsync(ct)}");

            int reservedQuantity = reservationResults[0] ? quantity10 : reservationResults[1] ? quantity11 : 0;
            if (reservedQuantity > 0)
            {
                bool released = await _reserveStock.ReleaseStockAsync(ProductId, reservedQuantity, ct);
                Console.WriteLine($"Release of {reservedQuantity} unit(s) succeeded: {released}");
                Console.WriteLine($"Stock after release: {await ReadProductStockAsync(ct)}");
            }
        }

        private int GetProductQuantity(Order order) => order.Items
            .Where(item => item.ProductId == ProductId)
            .Sum(item => item.Quantity);

        private async Task SetProductStockAsync(int quantity, CancellationToken ct)
        {
            await _jsonFileStore.UpdateAsync<List<Product>>(DataFiles.Products, products =>
            {
                Product product = products.FirstOrDefault(candidate => candidate.Id == ProductId)
                    ?? throw new InvalidOperationException($"Product #{ProductId} not found.");

                product.StockQuantity = quantity;
                return products;
            }, ct);
        }

        private async Task<int> ReadProductStockAsync(CancellationToken ct)
        {
            List<Product> products = await _jsonFileStore.ReadAsync<List<Product>>(DataFiles.Products, ct);
            Product product = products.FirstOrDefault(candidate => candidate.Id == ProductId)
                ?? throw new InvalidOperationException($"Product #{ProductId} not found.");
            return product.StockQuantity;
        }
    }
}
