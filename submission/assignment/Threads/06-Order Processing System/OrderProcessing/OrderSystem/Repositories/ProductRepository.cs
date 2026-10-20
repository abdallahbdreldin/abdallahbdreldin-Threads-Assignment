using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class ProductRepository : IProductRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public ProductRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(ProductRepository), $"query for Product #{id}", ct);

            List<Product> products = await _jsonFileStore.ReadAsync<List<Product>>(DataFiles.Products, ct);
            Product? product = products.FirstOrDefault(candidate => candidate.Id == id);

            if (product is null)
                Log.Error($"{nameof(ProductRepository)} -> Product #{id} not found");
            else
                Log.Success($"{nameof(ProductRepository)} -> Product #{id} found ({product.Name})");

            return product;
        }

        public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct)
        {
            await RepositoryLatency.BeginWriteAsync(nameof(ProductRepository), $"Reserving {qty} unit(s) of Product #{productId}", ct);

            bool reserved = false;
            await _jsonFileStore.UpdateAsync<List<Product>>(DataFiles.Products, products =>
            {
                Product? product = products.FirstOrDefault(candidate => candidate.Id == productId);
                if (product is not null && qty > 0 && product.StockQuantity >= qty)
                {
                    product.StockQuantity -= qty;
                    reserved = true;
                }

                return products;
            }, ct);

            await RepositoryLatency.CommitAsync(nameof(ProductRepository), ct);

            if (reserved)
                Log.Success($"{nameof(ProductRepository)} -> Reserved {qty} unit(s) of Product #{productId}");
            else
                Log.Error($"{nameof(ProductRepository)} -> Could not reserve {qty} unit(s) of Product #{productId}");

            return reserved;
        }
    }
}
