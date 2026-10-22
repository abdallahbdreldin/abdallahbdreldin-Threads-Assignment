using System.Text;
using System.Text.Json;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class ReportGenerator
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly string _dataDirectory;

        public ReportGenerator(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            string dataDirectory)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
            _dataDirectory = dataDirectory;
        }

        public async Task<Report> GenerateAsync(CancellationToken ct)
        {
            List<Order> orders = await _orderRepository.GetAllAsync(ct);

            List<Order> completedOrders = orders
                .Where(order => order.Status == OrderStatus.Completed)
                .ToList();

            Dictionary<int, int> unitsByProduct = completedOrders
                .SelectMany(order => order.Items)
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

            var productLookups = unitsByProduct.Select(async entry =>
            {
                Product? product = await _productRepository.GetByIdAsync(entry.Key, ct);
                return new
                {
                    ProductId = entry.Key,
                    Quantity = entry.Value,
                    Name = product?.Name
                };
            });

            var productSales = await Task.WhenAll(productLookups);
            var topProduct = productSales
                .OrderByDescending(sale => sale.Quantity)
                .ThenBy(sale => sale.ProductId)
                .FirstOrDefault();

            decimal totalRevenue = completedOrders.Sum(order => order.TotalAmount);

            int completedCount = completedOrders.Count;

            var report = new Report
            {
                TotalOrders = orders.Count,
                CompletedCount = completedCount,
                RejectedCount = orders.Count(order => order.Status == OrderStatus.Rejected),
                FailedCount = orders.Count(order => order.Status == OrderStatus.Failed),
                PaymentFailedCount = orders.Count(order => order.Status == OrderStatus.PaymentFailed),
                TotalRevenue = totalRevenue,
                AverageOrderValue = completedCount == 0 ? 0m : totalRevenue / completedCount,
                TopSellingProduct = topProduct?.Name,
                TopSellingQuantity = topProduct?.Quantity ?? 0,
                GeneratedAt = DateTime.Now
            };

            string reportDirectory = Path.Combine(_dataDirectory, "reports");
            Directory.CreateDirectory(reportDirectory);

            string reportPath = Path.Combine(reportDirectory, $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json");
            string json = JsonSerializer.Serialize(report, JsonFileStore.SerializerOptions);
            await File.WriteAllTextAsync(reportPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

            return report;
        }
    }
}
