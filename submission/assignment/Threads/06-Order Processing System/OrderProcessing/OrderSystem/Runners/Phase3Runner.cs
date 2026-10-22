using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;
using OrderSystem.Repositories;
using OrderSystem.Services;
using System.Diagnostics;

namespace OrderSystem.Runners
{
    public sealed class Phase3Runner
    {
        private readonly DataSeeder _seeder;
        private readonly IIncomingOrderRepository _incomingOrderRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly OrderProcessor _orderProcessor;

        public Phase3Runner(JsonFileStore jsonFileStore)
        {
            ArgumentNullException.ThrowIfNull(jsonFileStore);

            _seeder = new DataSeeder(jsonFileStore);
            _incomingOrderRepository = new IncomingOrderRepository(jsonFileStore);
            _orderRepository = new OrderRepository(jsonFileStore, new[] { 13 });

            var customerRepository = new CustomerRepository(jsonFileStore);
            var productRepository = new ProductRepository(jsonFileStore);
            var couponRepository = new CouponRepository(jsonFileStore);
            var customerService = new CustomerService(customerRepository);
            var inventoryService = new InventoryService(jsonFileStore);
            var pricingService = new PricingService(
                jsonFileStore,
                productRepository,
                customerRepository,
                couponRepository,
                Array.Empty<int>());
            IPaymentGateway paymentGateway = new PaymentGateway(
                jsonFileStore,
                timeoutOrderIds: new[] { 5 },
                declineOrderIds: new[] { 8 });
            IEmailService emailService = new EmailService(
                jsonFileStore,
                new Dictionary<int, int>
                {
                    [3] = 2,
                    [17] = int.MaxValue
                });

            _orderProcessor = new OrderProcessor(
                customerService,
                inventoryService,
                pricingService,
                paymentGateway,
                _orderRepository,
                emailService);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            await _seeder.ResetAsync(ct);
            List<Order> incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);

            var ordersToProcess = new List<Order>(incomingOrders.Count);
            var tasks = new List<Task<bool>>(incomingOrders.Count);

            foreach (Order order in incomingOrders)
            {
                if (ct.IsCancellationRequested)
                    break;

                ordersToProcess.Add(order);
                tasks.Add(_orderProcessor.ProcessOrderAsync(order, ct));
            }

            for (int index = ordersToProcess.Count; index < incomingOrders.Count; index++)
            {
                Order notStartedOrder = incomingOrders[index];
                notStartedOrder.Status = OrderStatus.Pending;
                notStartedOrder.FailureReason = "Cancelled";
                await _orderRepository.SaveAsync(notStartedOrder, CancellationToken.None);
            }

            for (int index = 0; index < tasks.Count; index++)
            {
                try
                {
                    await tasks[index];
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // The processor released stock, refunded any captured payment and saved Pending.
                }
                catch (Exception exception)
                {
                    Order order = ordersToProcess[index];
                    Log.Error($"Order #{order.Id} escaped processing: {exception.Message}");
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = exception.Message;
                    await _orderRepository.SaveAsync(order, CancellationToken.None);
                }
            }

            List<Order> savedOrders = await _orderRepository.GetAllAsync(CancellationToken.None);
            Console.WriteLine("===== Phase 3 Checkout Results =====");
            Console.WriteLine($"{"Order",6} {"Status",-15} {"Total",12}  Reason");
            foreach (Order order in savedOrders.OrderBy(order => order.Id))
                Console.WriteLine($"{order.Id,6} {order.Status,-15} {order.TotalAmount,12:F2}  {order.FailureReason}");

            Console.WriteLine($"orders.json contains {savedOrders.Count} records (expected 20).");
            if (savedOrders.Count != 20)
                throw new InvalidOperationException($"Expected 20 saved orders, found {savedOrders.Count}.");
        }

        public async Task RunLiveCheckoutAsync(CancellationToken ct)
        {
            await _seeder.ResetAsync(ct);
            List<Order> orders = await _incomingOrderRepository.GetAllAsync(ct);
            var orderByTask = new Dictionary<Task<bool>, (Order Order, Stopwatch Stopwatch)>();

            foreach (Order order in orders)
            {
                ct.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                Task<bool> task = _orderProcessor.ProcessOrderAsync(order, ct);
                orderByTask.Add(task, (order, stopwatch));
            }

            int completed = 0;
            bool wasCancelled = false;
            await foreach (Task<bool> completedTask in Task.WhenEach(orderByTask.Keys))
            {
                (Order order, Stopwatch stopwatch) = orderByTask[completedTask];
                try
                {
                    await completedTask;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    wasCancelled = true;
                }
                catch (Exception exception)
                {
                    Log.Error($"Order #{order.Id} escaped processing: {exception.Message}");
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = exception.Message;
                    await _orderRepository.SaveAsync(order, CancellationToken.None);
                }

                stopwatch.Stop();
                completed++;
                string reason = string.IsNullOrWhiteSpace(order.FailureReason) ? string.Empty : $" ({order.FailureReason})";
                Console.WriteLine($"[{completed,2}/20] Order #{order.Id} -> {order.Status}{reason} after {stopwatch.Elapsed.TotalSeconds:F1} s");
            }

            List<Order> savedOrders = await _orderRepository.GetAllAsync(CancellationToken.None);
            PrintStatusCounts(savedOrders);
            if (savedOrders.Count != 20)
                throw new InvalidOperationException($"Expected 20 saved orders, found {savedOrders.Count}.");
            if (wasCancelled)
                throw new OperationCanceledException(ct);
        }

        private static void PrintStatusCounts(IEnumerable<Order> orders)
        {
            Console.WriteLine("Order counts by status:");
            foreach (var group in orders.GroupBy(order => order.Status).OrderBy(group => group.Key))
                Console.WriteLine($"  {group.Key}: {group.Count()}");
        }
    }
}
