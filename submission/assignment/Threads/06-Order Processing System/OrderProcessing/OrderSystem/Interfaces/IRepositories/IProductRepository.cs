using OrderSystem.Models;

namespace OrderSystem.Interfaces.IRepositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id, CancellationToken ct);
        Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct);
    }
}
