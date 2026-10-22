using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Models;
using OrderSystem.Repositories;
using OrderSystem.Logs;

namespace OrderSystem.Services
{
    public class ReserveStock
    {
        private readonly IJsonFileStore _jsonFileStore;

        public ReserveStock(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore;
        }

        public async Task ReserveStockUnsafeAsync(int productId , int quantity, CancellationToken ct)
        {
            var products = await _jsonFileStore.ReadAsync<List<Product>>(DataFiles.Products, ct);

            var product = products.FirstOrDefault(p => p.Id == productId);

            if (product is null) throw new NullReferenceException($"Product #{productId} Not Found");

            var stokQuantity = product.StockQuantity;

            await RepositoryLatency.BeginWriteAsync(nameof(ReserveStock), $"Reserve Stock {product.Name} With ID #{productId} ", ct);

            product.StockQuantity = stokQuantity - quantity;

            await _jsonFileStore.WriteAsync(DataFiles.Products, products, ct);
            await RepositoryLatency.CommitAsync(nameof(ReserveStock), ct);
        }

        public async Task<bool> ReserveStockAsync(int productId, int quantity, CancellationToken ct)
        {
            if (quantity <= 0)
                return false;

            await RepositoryLatency.BeginWriteAsync(nameof(ReserveStock), $"Safely reserving {quantity} unit(s) of Product #{productId}", ct);

            bool reserved = false;
            await _jsonFileStore.UpdateAsync<List<Product>>(DataFiles.Products, products =>
            {
                Product? product = products.FirstOrDefault(candidate => candidate.Id == productId);
                if (product is not null && product.StockQuantity >= quantity)
                {
                    product.StockQuantity -= quantity;
                    reserved = true;
                }

                return products;
            }, ct);

            await RepositoryLatency.CommitAsync(nameof(ReserveStock), ct);

            if (reserved)
                Log.Success($"{nameof(ReserveStock)} -> Reserved {quantity} unit(s) of Product #{productId}");
            else
                Log.Error($"{nameof(ReserveStock)} -> Could not reserve {quantity} unit(s) of Product #{productId}");

            return reserved;
        }

        public async Task<bool> ReleaseStockAsync(int productId, int quantity, CancellationToken ct)
        {
            if (quantity <= 0)
                return false;

            await RepositoryLatency.BeginWriteAsync(nameof(ReserveStock), $"Releasing {quantity} unit(s) of Product #{productId}", ct);

            bool released = false;
            await _jsonFileStore.UpdateAsync<List<Product>>(DataFiles.Products, products =>
            {
                Product? product = products.FirstOrDefault(candidate => candidate.Id == productId);
                if (product is not null)
                {
                    product.StockQuantity += quantity;
                    released = true;
                }

                return products;
            }, ct);

            await RepositoryLatency.CommitAsync(nameof(ReserveStock), ct);

            if (released)
                Log.Success($"{nameof(ReserveStock)} -> Released {quantity} unit(s) of Product #{productId}");
            else
                Log.Error($"{nameof(ReserveStock)} -> Could not release {quantity} unit(s) of Product #{productId}");

            return released;
        }
    }
}
