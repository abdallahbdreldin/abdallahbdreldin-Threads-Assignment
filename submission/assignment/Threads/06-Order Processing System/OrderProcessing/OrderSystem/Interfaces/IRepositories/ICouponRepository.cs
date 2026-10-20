using OrderSystem.Models;

namespace OrderSystem.Interfaces.IRepositories
{
    public interface ICouponRepository
    {
        Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct);
    }
}
