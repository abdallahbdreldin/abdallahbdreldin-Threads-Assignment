using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Logs;
using OrderSystem.Models;
using OrderSystem.Repositories;

namespace OrderSystem.Services
{
    public sealed class InventoryService
    {
        private readonly IJsonFileStore _jsonFileStore;

        public InventoryService(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            Dictionary<int, int> requestedQuantities = GetQuantities(order);

            await RepositoryLatency.BeginWriteAsync(nameof(InventoryService), $"Reserving stock for Order #{order.Id}", ct);

            bool reserved = false;
            string? unavailableProductName = null;
            await _jsonFileStore.UpdateAsync<List<Product>>(DataFiles.Products, products =>
            {
                foreach ((int productId, int quantity) in requestedQuantities)
                {
                    Product? product = products.FirstOrDefault(candidate => candidate.Id == productId);
                    if (product is null || product.StockQuantity < quantity)
                    {
                        unavailableProductName = product?.Name ?? $"Product #{productId}";
                        return products;
                    }
                }

                foreach ((int productId, int quantity) in requestedQuantities)
                {
                    Product product = products.First(candidate => candidate.Id == productId);
                    product.StockQuantity -= quantity;
                }

                reserved = true;
                return products;
            }, ct);

            await RepositoryLatency.CommitAsync(nameof(InventoryService), ct);

            if (reserved)
            {
                Log.Success($"{nameof(InventoryService)} -> Reserved stock for Order #{order.Id}");
                return true;
            }

            order.FailureReason = $"Insufficient stock for '{unavailableProductName ?? "unknown product"}'";
            Log.Error($"{nameof(InventoryService)} -> {order.FailureReason} for Order #{order.Id}");
            return false;
        }

        public async Task ReleaseStockAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            Dictionary<int, int> releasedQuantities = GetQuantities(order);

            await RepositoryLatency.BeginWriteAsync(nameof(InventoryService), $"Releasing stock for Order #{order.Id}", ct);

            await _jsonFileStore.UpdateAsync<List<Product>>(DataFiles.Products, products =>
            {
                foreach (int productId in releasedQuantities.Keys)
                {
                    if (!products.Any(product => product.Id == productId))
                        throw new InvalidOperationException($"Product #{productId} was not found while releasing Order #{order.Id} stock.");
                }

                foreach ((int productId, int quantity) in releasedQuantities)
                {
                    Product product = products.First(candidate => candidate.Id == productId);
                    product.StockQuantity += quantity;
                }

                return products;
            }, ct);

            await RepositoryLatency.CommitAsync(nameof(InventoryService), ct);
            Log.Success($"{nameof(InventoryService)} -> Released stock for Order #{order.Id}");
        }

        private static Dictionary<int, int> GetQuantities(Order order)
        {
            if (order.Items.Any(item => item.Quantity <= 0))
                throw new ArgumentOutOfRangeException(nameof(order), "Order item quantities must be positive.");

            return order.Items
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        }
    }
}
