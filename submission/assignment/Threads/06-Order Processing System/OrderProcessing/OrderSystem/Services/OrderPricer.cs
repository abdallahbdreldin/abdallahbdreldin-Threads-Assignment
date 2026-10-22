using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class OrderPricer
    {
        private readonly PricingService _pricingService;

        public OrderPricer(PricingService pricingService)
        {
            _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        }

        public async Task PriceOrderAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);

            order.SubTotal = await _pricingService.CalculateSubTotalAsync(order, ct);

            Task<decimal> discountTask = _pricingService.GetDiscountAsync(order, ct);
            Task<decimal> shippingTask = _pricingService.CalculateShippingCostAsync(order, ct);

            order.Discount = await discountTask;
            order.ShippingCost = await shippingTask;

            order.TotalAmount = PricingService.RoundMoney(order.SubTotal - order.Discount + order.ShippingCost);
        }

        public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);

            order.SubTotal = await _pricingService.CalculateSubTotalAsync(order, ct);

            order.Discount = await _pricingService.GetDiscountAsync(order, ct);

            order.ShippingCost = await _pricingService.CalculateShippingCostAsync(order, ct);

            order.TotalAmount = PricingService.RoundMoney(order.SubTotal - order.Discount + order.ShippingCost);
        }
    }
}
