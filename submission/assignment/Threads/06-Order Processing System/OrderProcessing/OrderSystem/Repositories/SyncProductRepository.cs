using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class SyncProductRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public SyncProductRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public Product? GetById(int id, CancellationToken ct)
        {
            SyncRepositoryLatency.Query(nameof(SyncProductRepository), $"query for Product #{id}", ct);

            List<Product> products = _jsonFileStore.Read<List<Product>>(DataFiles.Products);

            Product? product = products.FirstOrDefault(candidate => candidate.Id == id);

            if (product is null)
                Log.Error($"{nameof(SyncProductRepository)} -> Product #{id} not found");
            else
                Log.Success($"{nameof(SyncProductRepository)} -> Product #{id} found ({product.Name})");

            return product;
        }
    }
}
