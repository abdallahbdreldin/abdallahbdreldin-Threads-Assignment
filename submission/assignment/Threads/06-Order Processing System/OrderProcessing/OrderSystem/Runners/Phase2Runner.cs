using System.Diagnostics;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;
using OrderSystem.Repositories;
using OrderSystem.Services;
using OrderSystem.Validators;

namespace OrderSystem.Runners
{
    public sealed class Phase2Runner
    {
        private readonly DataSeeder _seeder;
        private readonly IIncomingOrderRepository _incomingOrderRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly OrderValidator _orderValidator;
        private readonly RaceConditionDemo _raceConditionDemo;
        private readonly PricingComparisonDemo _pricingComparisonDemo;
        private readonly OrderProcessorV2 _orderProcessor;
        private readonly ReserveStock _reserveStock;
        private readonly ReportGenerator _reportGenerator;

        public Phase2Runner(JsonFileStore jsonFileStore)
        {
            ArgumentNullException.ThrowIfNull(jsonFileStore);

            _seeder = new DataSeeder(jsonFileStore);
            _incomingOrderRepository = new IncomingOrderRepository(jsonFileStore);
            _orderRepository = new OrderRepository(jsonFileStore);

            var customerRepository = new CustomerRepository(jsonFileStore);
            var productRepository = new ProductRepository(jsonFileStore);
            var couponRepository = new CouponRepository(jsonFileStore);
            _reserveStock = new ReserveStock(jsonFileStore);

            var pricingService = new PricingService(
                jsonFileStore,
                productRepository,
                customerRepository,
                couponRepository,
                new[] { 7 });

            var orderPricer = new OrderPricer(pricingService);
            var invoiceGenerator = new InvoiceGenerator(jsonFileStore.DataDirectory);

            _orderValidator = new OrderValidator(
                customerRepository,
                productRepository,
                couponRepository,
                _orderRepository);

            _raceConditionDemo = new RaceConditionDemo(jsonFileStore, _orderRepository);
            _pricingComparisonDemo = new PricingComparisonDemo(_incomingOrderRepository, orderPricer);

            _orderProcessor = new OrderProcessorV2(
                _reserveStock,
                productRepository,
                orderPricer,
                invoiceGenerator,
                _orderRepository);

            _reportGenerator = new ReportGenerator(
                _orderRepository,
                productRepository,
                jsonFileStore.DataDirectory);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            await _seeder.ResetAsync(ct);
            await PrepareRaceDemoOrdersAsync(ct);
            await _raceConditionDemo.RunAsync(ct);

            await _seeder.ResetAsync(ct);
            await _pricingComparisonDemo.RunAsync(ct);

            List<Order> incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);
            List<Task> validationTasks = incomingOrders
                .Select(order => _orderValidator.ValidateOrderAsync(order, ct))
                .ToList();
            await Task.WhenAll(validationTasks);

            List<Order> validatedOrders = await _orderRepository.GetByStatusAsync(OrderStatus.Validated, ct);
            var orderTasks = new List<(Order Order, Task<bool> Task)>(validatedOrders.Count);
            var stopwatch = Stopwatch.StartNew();

            foreach (Order order in validatedOrders)
                orderTasks.Add((order, _orderProcessor.ProcessValidatedOrderAsync(order, ct)));

            if (orderTasks.Count > 0)
                Console.WriteLine($"First processing task status: {orderTasks[0].Task.Status}");

            await Task.Delay(1000, ct);
            foreach ((Order order, Task<bool> task) in orderTasks)
                Console.WriteLine($"Order #{order.Id} task status after 1 second: {task.Status}");

            foreach ((Order order, Task<bool> task) in orderTasks)
            {
                try
                {
                    await task;
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Order #{order.Id} task failed with status {task.Status}: {exception.Message}");
                    await ReleaseOrderStockAsync(order, ct);

                    order.Status = OrderStatus.Failed;
                    order.FailureReason = exception.Message;
                    await _orderRepository.SaveAsync(order, ct);
                }
            }

            stopwatch.Stop();
            foreach ((Order order, Task<bool> task) in orderTasks)
                Console.WriteLine($"Order #{order.Id} final task status: {task.Status}");
            Console.WriteLine($"Concurrent order processing time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

            List<Order> savedOrders = await _orderRepository.GetAllAsync(ct);
            Console.WriteLine($"orders.json contains {savedOrders.Count} records (expected 20).");
            if (savedOrders.Count != 20)
                throw new InvalidOperationException($"Expected 20 saved orders, found {savedOrders.Count}.");

            Task<Report> reportTask = _reportGenerator.GenerateAsync(ct);
            Console.WriteLine("Generating report...");
            Report report = await reportTask;
            PrintReport(report);
        }

        private async Task PrepareRaceDemoOrdersAsync(CancellationToken ct)
        {
            List<Order> incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);
            foreach (int orderId in new[] { 10, 11 })
            {
                Order order = incomingOrders.FirstOrDefault(o => o.Id == orderId)
                    ?? throw new InvalidOperationException($"Incoming Order #{orderId} not found.");
                await _orderRepository.SaveAsync(order, ct);
            }
        }

        private async Task ReleaseOrderStockAsync(Order order, CancellationToken ct)
        {
            foreach (OrderItem item in order.Items)
                await _reserveStock.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
        }

        private static void PrintReport(Report report)
        {
            Console.WriteLine("===== Phase 2 Report =====");
            Console.WriteLine($"Total orders: {report.TotalOrders}");
            Console.WriteLine($"Completed: {report.CompletedCount}");
            Console.WriteLine($"Rejected: {report.RejectedCount}");
            Console.WriteLine($"Failed: {report.FailedCount}");
            Console.WriteLine($"Payment failed: {report.PaymentFailedCount}");
            Console.WriteLine($"Total revenue: {report.TotalRevenue:F2}");
            Console.WriteLine($"Average order value: {report.AverageOrderValue:F2}");
            Console.WriteLine($"Top selling product: {report.TopSellingProduct ?? "(none)"} ({report.TopSellingQuantity} units)");
        }
    }
}
