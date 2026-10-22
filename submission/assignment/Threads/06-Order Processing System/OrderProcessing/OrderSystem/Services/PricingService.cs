using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class PricingService
    {
        private readonly IJsonFileStore _jsonFileStore;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICouponRepository _couponRepository;
        private readonly HashSet<int> _failShippingForOrderIds;

        public PricingService(
            IJsonFileStore jsonFileStore,
            IProductRepository productRepository,
            ICustomerRepository customerRepository,
            ICouponRepository couponRepository,
            IEnumerable<int>? failShippingForOrderIds)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _couponRepository = couponRepository ?? throw new ArgumentNullException(nameof(couponRepository));
            _failShippingForOrderIds = failShippingForOrderIds?.ToHashSet() ?? new HashSet<int>();
        }

        public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            decimal subTotal = 0m;

            foreach (OrderItem item in order.Items)
            {
                Product? product = await _productRepository.GetByIdAsync(item.ProductId, ct);
                if (product is null)
                    throw new InvalidOperationException($"Product #{item.ProductId} not found");

                item.UnitPrice = product.Price;
                subTotal += item.UnitPrice * item.Quantity;
            }

            return RoundMoney(subTotal);
        }

        public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            await SimulateDiscountApiAsync(ct);

            Customer? customer = await _customerRepository.GetByIdAsync(order.CustomerId, ct);
            if (customer is null)
                throw new InvalidOperationException($"Customer #{order.CustomerId} not found");

            int couponPercent = 0;
            if (!string.IsNullOrWhiteSpace(order.CouponCode))
            {
                Coupon? coupon = await _couponRepository.GetByCodeAsync(order.CouponCode, ct);
                if (coupon is null)
                    throw new InvalidOperationException($"Coupon '{order.CouponCode}' does not exist");
                if (coupon.ExpiryDate < DateTime.Today)
                    throw new InvalidOperationException($"Coupon '{order.CouponCode}' has expired");

                couponPercent = coupon.DiscountPercent;
            }

            int loyaltyPercent = customer.LoyaltyTier switch
            {
                LoyaltyTier.Bronze => 0,
                LoyaltyTier.Silver => 5,
                LoyaltyTier.Gold => 10,
                _ => 0
            };
            int discountPercent = Math.Min(loyaltyPercent + couponPercent, 30);

            return RoundMoney(order.SubTotal * discountPercent / 100m);
        }

        public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            await SimulateShippingHandshakeAsync(ct);

            if (_failShippingForOrderIds.Contains(order.Id))
                throw new ShippingProviderUnavailableException("Shipping provider unavailable");

            await SimulateShippingRequestAsync(ct);
            await SimulateShippingResponseAsync(ct);

            List<Product> products = await _jsonFileStore.ReadAsync<List<Product>>(DataFiles.Products, ct);

            if (order.SubTotal >= 1000m)
                return 0m;

            int totalQuantity = order.Items.Sum(item => item.Quantity);
            decimal totalWeight = 0m;
            foreach (OrderItem item in order.Items)
            {
                Product? product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId);
                if (product is null)
                    throw new InvalidOperationException($"Product #{item.ProductId} not found");

                totalWeight += product.WeightKg * item.Quantity;
            }

            decimal shippingCost = 5.00m + 1.50m * totalQuantity + 2.00m * totalWeight;
            return RoundMoney(shippingCost);
        }

        public async Task<decimal> CalculateTotalPrice(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            order.SubTotal = await CalculateSubTotalAsync(order, ct);
            order.Discount = await GetDiscountAsync(order, ct);
            order.ShippingCost = await CalculateShippingCostAsync(order, ct);
            order.TotalAmount = RoundMoney(order.SubTotal - order.Discount + order.ShippingCost);
            return order.TotalAmount;
        }

        internal static decimal RoundMoney(decimal amount) =>
            Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        private static async Task SimulateDiscountApiAsync(CancellationToken ct)
        {
            Log.Info("PricingService -> Discount API DNS/handshake...");
            await Task.Delay(150, ct);
            Log.Info("PricingService -> Discount API sending request...");
            await Task.Delay(300, ct);
            Log.Info("PricingService -> Discount API waiting for response...");
            await Task.Delay(Random.Shared.Next(500, 1500 + 1), ct);
        }

        private static async Task SimulateShippingHandshakeAsync(CancellationToken ct)
        {
            Log.Info("PricingService -> Shipping API DNS/handshake...");
            await Task.Delay(150, ct);
        }

        private static async Task SimulateShippingRequestAsync(CancellationToken ct)
        {
            Log.Info("PricingService -> Shipping API sending request...");
            await Task.Delay(300, ct);
        }

        private static async Task SimulateShippingResponseAsync(CancellationToken ct)
        {
            Log.Info("PricingService -> Shipping API waiting for response...");
            await Task.Delay(Random.Shared.Next(500, 1500 + 1), ct);
        }
    }
}
