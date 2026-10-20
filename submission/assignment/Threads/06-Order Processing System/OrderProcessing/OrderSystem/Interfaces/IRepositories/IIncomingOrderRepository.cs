using OrderSystem.Models;

namespace OrderSystem.Interfaces.IRepositories
{
    public interface IIncomingOrderRepository
    {
        Task<List<Order>> GetAllAsync(CancellationToken ct);
    }
}
