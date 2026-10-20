using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class CouponRepository : ICouponRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public CouponRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(code);

            await RepositoryLatency.QueryAsync(nameof(CouponRepository), $"query for Coupon '{code}'", ct);

            List<Coupon> coupons = await _jsonFileStore.ReadAsync<List<Coupon>>(DataFiles.Coupons, ct);
            Coupon? coupon = coupons.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));

            if (coupon is null)
                Log.Error($"{nameof(CouponRepository)} -> Coupon '{code}' not found");
            else
                Log.Success($"{nameof(CouponRepository)} -> Coupon '{coupon.Code}' found");

            return coupon;
        }
    }
}
