using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;
using OrderSystem.Repositories;
using OrderSystem.Validators;
using System.Diagnostics;

namespace OrderSystem.Runners
{
    public sealed class Phase1Runner
    {
        private readonly DataSeeder _seeder;
        private readonly IIncomingOrderRepository _incomingOrderRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly SyncOrderValidator _syncValidator;
        private readonly OrderValidator _asyncValidator;

        public Phase1Runner(JsonFileStore jsonFileStore)
        {
            ArgumentNullException.ThrowIfNull(jsonFileStore);

            _seeder = new DataSeeder(jsonFileStore);

            _incomingOrderRepository = new IncomingOrderRepository(jsonFileStore);
            _orderRepository = new OrderRepository(jsonFileStore);

            var syncCustomers = new SyncCustomerRepository(jsonFileStore);
            var syncProducts = new SyncProductRepository(jsonFileStore);
            var syncCoupons = new SyncCouponRepository(jsonFileStore);
            _syncValidator = new SyncOrderValidator(syncCustomers, syncProducts, syncCoupons);

            var customers = new CustomerRepository(jsonFileStore);
            var products = new ProductRepository(jsonFileStore);
            var coupons = new CouponRepository(jsonFileStore);
            _asyncValidator = new OrderValidator(customers, products, coupons, _orderRepository);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            int totalOrders = 0;
            int finishedOrders = 0;
            Order? currentOrder = null;
            bool asyncRunStarted = false;
            double syncSeconds = 0;
            double asyncSeconds = 0;
            var stopwatch = new Stopwatch();

            try
            {
                await _seeder.ResetAsync(ct);

                List<Order> incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);
                totalOrders = incomingOrders.Count;

                stopwatch.Start();

                foreach (Order order in incomingOrders)
                    _syncValidator.ValidateOrder(order, ct);

                stopwatch.Stop();
                syncSeconds = stopwatch.Elapsed.TotalSeconds;

                await _seeder.ResetAsync(ct);

                incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);
                totalOrders = incomingOrders.Count;

                asyncRunStarted = true;
                stopwatch.Restart();

                foreach (Order order in incomingOrders)
                {
                    ct.ThrowIfCancellationRequested();
                    currentOrder = order;
                    await _asyncValidator.ValidateOrderAsync(order, ct);
                    finishedOrders++;
                    currentOrder = null;
                }
                stopwatch.Stop();
                asyncSeconds = stopwatch.Elapsed.TotalSeconds;

                List<Order> validatedOrders = await _orderRepository.GetByStatusAsync(OrderStatus.Validated, ct);
                List<Order> rejectedOrders = await _orderRepository.GetByStatusAsync(OrderStatus.Rejected, ct);
                List<Order> savedOrders = await _orderRepository.GetAllAsync(ct);

                PrintSummary(savedOrders.Count, validatedOrders, rejectedOrders, syncSeconds, asyncSeconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                stopwatch.Stop();

                if (asyncRunStarted && currentOrder is not null)
                {
                    currentOrder.Status = OrderStatus.Pending;
                    currentOrder.FailureReason = "Cancelled";
                    await _orderRepository.SaveAsync(currentOrder, CancellationToken.None);
                }

                int cancelledOrders = Math.Max(0, totalOrders - finishedOrders);
                Console.WriteLine($"Run cancelled — {finishedOrders} orders finished, {cancelledOrders} cancelled.");
            }
        }

        private static void PrintSummary(
            int totalOrders,
            IReadOnlyCollection<Order> validatedOrders,
            IReadOnlyCollection<Order> rejectedOrders,
            double syncSeconds,
            double asyncSeconds)
        {
            Console.WriteLine("===== Validation Summary =====");

            Console.WriteLine($"Total Orders: {totalOrders}");
            Console.WriteLine($"Validated:    {validatedOrders.Count}");
            Console.WriteLine($"Rejected:     {rejectedOrders.Count}");

            foreach (Order order in rejectedOrders.OrderBy(order => order.Id))
                Console.WriteLine($"  - Order #{order.Id}:  {order.FailureReason}");

            Console.WriteLine($"Sync Time:  {syncSeconds:F1} seconds");
            Console.WriteLine($"Async Time: {asyncSeconds:F1} seconds");
            Console.WriteLine($"orders.json now contains {totalOrders} records.");
        }
    }
}
