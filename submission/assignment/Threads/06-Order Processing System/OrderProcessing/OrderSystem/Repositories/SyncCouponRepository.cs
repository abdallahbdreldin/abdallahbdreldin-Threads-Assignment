using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class SyncCouponRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public SyncCouponRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public Coupon? GetByCode(string code, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(code);

            SyncRepositoryLatency.Query(nameof(SyncCouponRepository), $"query for Coupon '{code}'", ct);

            List<Coupon> coupons = _jsonFileStore.Read<List<Coupon>>(DataFiles.Coupons);

            Coupon? coupon = coupons.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));

            if (coupon is null)
                Log.Error($"{nameof(SyncCouponRepository)} -> Coupon '{code}' not found");
            else
                Log.Success($"{nameof(SyncCouponRepository)} -> Coupon '{coupon.Code}' found");

            return coupon;
        }
    }
}
