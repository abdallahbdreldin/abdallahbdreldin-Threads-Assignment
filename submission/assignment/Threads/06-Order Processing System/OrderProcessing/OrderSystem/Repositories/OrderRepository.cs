using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class OrderRepository : IOrderRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public OrderRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public async Task SaveAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            await RepositoryLatency.BeginWriteAsync(nameof(OrderRepository), $"Saving Order #{order.Id}", ct);

            await _jsonFileStore.UpdateAsync<List<Order>>(DataFiles.Orders, orders =>
            {
                int existingIndex = orders.FindIndex(existing => existing.Id == order.Id);
                if (existingIndex >= 0)
                    orders[existingIndex] = order;
                else
                    orders.Add(order);
                return orders;
            }, ct);

            await RepositoryLatency.CommitAsync(nameof(OrderRepository), ct);
            Log.Success($"{nameof(OrderRepository)} -> Order #{order.Id} saved");
        }

        public async Task<List<Order>> GetAllAsync(CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(OrderRepository), "query for all orders", ct);

            List<Order> orders = await _jsonFileStore.ReadAsync<List<Order>>(DataFiles.Orders, ct);

            Log.Success($"{nameof(OrderRepository)} -> Loaded {orders.Count} order(s)");

            return orders;
        }

        public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(OrderRepository), $"query for orders with status {status}", ct);

            List<Order> orders = await _jsonFileStore.ReadAsync<List<Order>>(DataFiles.Orders, ct);

            List<Order> matchingOrders = orders.Where(order => order.Status == status).ToList();

            Log.Success($"{nameof(OrderRepository)} -> Loaded {matchingOrders.Count} order(s) with status {status}");

            return matchingOrders;
        }
    }
}
