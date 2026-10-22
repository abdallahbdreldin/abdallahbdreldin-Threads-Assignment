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

    }
}
