using OrderSystem.Models;

namespace OrderSystem.Interfaces.IRepositories
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync(CancellationToken ct);
        Task<Product?> GetByIdAsync(int id, CancellationToken ct);
    }
}
