using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class IncomingOrderRepository : IIncomingOrderRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public IncomingOrderRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public async Task<List<Order>> GetAllAsync(CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(IncomingOrderRepository), "query for incoming orders", ct);

            List<Order> orders = await _jsonFileStore.ReadAsync<List<Order>>(DataFiles.IncomingOrders, ct);

            Log.Success($"{nameof(IncomingOrderRepository)} -> Loaded {orders.Count} incoming order(s)");

            return orders;
        }
    }
}
