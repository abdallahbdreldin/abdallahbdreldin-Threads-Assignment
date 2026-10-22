using System.Diagnostics;
using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class ShippingCarrier : IShippingCarrier
    {
        private readonly IJsonFileStore _store;
        private readonly int _minimumDelayMs;
        private readonly int _maximumDelayMs;
        private readonly decimal _priceFactor;
        private readonly bool _failsForEvenOrders;
        private readonly HashSet<int> _failForOrderIds;

        public string Name { get; }

        public ShippingCarrier(
            string name,
            IJsonFileStore store,
            int minimumDelayMs,
            int maximumDelayMs,
            decimal priceFactor,
            bool failsForEvenOrders = false,
            IEnumerable<int>? failForOrderIds = null)
        {
            Name = name;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _minimumDelayMs = minimumDelayMs;
            _maximumDelayMs = maximumDelayMs;
            _priceFactor = priceFactor;
            _failsForEvenOrders = failsForEvenOrders;
            _failForOrderIds = failForOrderIds?.ToHashSet() ?? new HashSet<int>();
        }

        public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await Task.Delay(150, ct);
                await Task.Delay(Random.Shared.Next(_minimumDelayMs, _maximumDelayMs + 1), ct);
                if (_failsForEvenOrders && order.Id % 2 == 0 || _failForOrderIds.Contains(order.Id))
                    throw new ShippingProviderUnavailableException($"{Name} is unavailable for Order #{order.Id}");

                decimal price = 0m;
                if (order.SubTotal < 1000m)
                {
                    List<Product> products = await _store.ReadAsync<List<Product>>(DataFiles.Products, ct);
                    int totalQuantity = order.Items.Sum(item => item.Quantity);
                    decimal totalWeight = order.Items.Sum(item =>
                    {
                        Product product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId)
                            ?? throw new InvalidOperationException($"Product #{item.ProductId} not found");
                        return product.WeightKg * item.Quantity;
                    });
                    price = 5.00m + 1.50m * totalQuantity + 2.00m * totalWeight;
                }

                return Math.Round(price * _priceFactor, 2, MidpointRounding.AwayFromZero);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Log.Info($"[{Name}] cancelled");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Log.Info($"[{Name}] finished after {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }
}
