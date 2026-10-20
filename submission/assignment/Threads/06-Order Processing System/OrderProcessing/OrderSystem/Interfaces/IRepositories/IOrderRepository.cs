using OrderSystem.Models;

namespace OrderSystem.Interfaces.IRepositories
{
    public interface IOrderRepository
    {
        Task SaveAsync(Order order, CancellationToken ct);
        Task<List<Order>> GetAllAsync(CancellationToken ct);
        Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct);
    }
}
