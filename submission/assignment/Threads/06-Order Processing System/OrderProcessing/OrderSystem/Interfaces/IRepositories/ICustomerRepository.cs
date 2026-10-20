using OrderSystem.Models;

namespace OrderSystem.Interfaces.IRepositories
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(int id, CancellationToken ct);
    }
}
