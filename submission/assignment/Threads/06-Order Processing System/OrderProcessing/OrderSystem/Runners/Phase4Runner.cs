using System.Collections.Concurrent;
using System.Diagnostics;
using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;
using OrderSystem.Services;

namespace OrderSystem.Runners
{
    public sealed class Phase4Runner
    {
        private readonly JsonFileStore _store;

        public Phase4Runner(JsonFileStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

        public async Task RunAsync(CancellationToken ct)
        {
            Console.WriteLine("===== Task 10.1: Live checkout progress =====");
            await new Phase3Runner(_store).RunLiveCheckoutAsync(ct);

            Console.WriteLine("===== Task 10.2: Customer dashboard =====");
            await RunDashboardAsync(3, ct);
            await RunDashboardAsync(99, ct);

            Console.WriteLine("===== Task 10.3: Fastest shipping quote =====");
            await RunShippingQuoteAsync(ct);

            Console.WriteLine("===== Task 10.4A: Fraud scoring =====");
            RunFraudScoring(ct);

            Console.WriteLine("===== Task 10.4B: Email resend =====");
            await RunEmailResendAsync(ct);
        }

        private async Task RunDashboardAsync(int customerId, CancellationToken ct)
        {
            var service = new CustomerDashboardService(_store, new[] { 99 });
            var stopwatch = Stopwatch.StartNew();
            try
            {
                CustomerDashboard dashboard = await service.LoadAsync(customerId, ct);
                stopwatch.Stop();
                string orders = string.Join(", ", dashboard.Orders.Select(order => $"#{order.Id} {order.Status}"));
                string payments = string.Join(", ", dashboard.Payments.Select(payment => $"#{payment.OrderId} {payment.Status}"));
                Console.WriteLine($"{dashboard.Customer.Name} — {dashboard.Orders.Count} orders ({orders}), {dashboard.Payments.Count} payments ({payments}), {dashboard.Emails.Count} email(s) in {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception exception)
            {
                stopwatch.Stop();

                Console.WriteLine($"Caught: {exception.Message}");

                IEnumerable<Exception> failures = exception is AggregateException aggregate
                    ? aggregate.Flatten().InnerExceptions
                    : new[] { exception };

                foreach (Exception failure in failures)
                    Console.WriteLine($"Failed: {failure.Message}");
                Console.WriteLine($"Dashboard loads finished in {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private async Task RunShippingQuoteAsync(CancellationToken ct)
        {
            List<Order> orders = await _store.ReadAsync<List<Order>>(DataFiles.Orders, ct);

            Order order = orders.FirstOrDefault(candidate => candidate.Id == 1)
                ?? throw new InvalidOperationException("Order #1 was not found for the shipping quote demo.");

            var carriers = new IShippingCarrier[]
            {
                new ShippingCarrier("FastShip", _store, 500, 1500, 1.00m),
                new ShippingCarrier("CairoExpress", _store, 300, 2000, 0.95m, failsForEvenOrders: true),
                new ShippingCarrier("NileCourier", _store, 800, 1200, 1.10m)
            };

            var service = new ShippingQuoteService(carriers);

            ShippingQuote quote = await service.GetFastestQuoteAsync(order, ct);

            Console.WriteLine($"Order #{order.Id}: {quote.Carrier} quoted {quote.Price:F2} after {quote.ElapsedMs} ms");
        }

        private static void RunFraudScoring(CancellationToken ct)
        {
            List<Order> orders = Enumerable.Range(1, 10_000).Select(id => new Order { Id = id }).ToList();
            var sequentialStopwatch = Stopwatch.StartNew();
            List<int> sequentialFlagged = new();
            foreach (Order order in orders)
            {
                ct.ThrowIfCancellationRequested();
                if (Score(order.Id) >= 90)
                    sequentialFlagged.Add(order.Id);
            }
            sequentialStopwatch.Stop();

            var parallelFlagged = new ConcurrentBag<int>();
            var threadIds = new ConcurrentDictionary<int, byte>();
            var parallelStopwatch = Stopwatch.StartNew();
            Parallel.ForEach(orders, new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, order =>
            {
                ct.ThrowIfCancellationRequested();
                threadIds.TryAdd(Environment.CurrentManagedThreadId, 0);
                if (Score(order.Id) >= 90)
                    parallelFlagged.Add(order.Id);
            });
            parallelStopwatch.Stop();

            int[] expected = sequentialFlagged.Order().ToArray();
            int[] actual = parallelFlagged.Order().ToArray();
            if (!expected.SequenceEqual(actual) || actual.Length != 1005)
                throw new InvalidOperationException("Sequential and parallel fraud scoring returned different results.");

            double speedup = parallelStopwatch.Elapsed.TotalMilliseconds == 0
                ? 0
                : sequentialStopwatch.Elapsed.TotalMilliseconds / parallelStopwatch.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Flagged: {actual.Length}; first five: {string.Join(", ", actual.Take(5))}");
            Console.WriteLine($"Sequential: {sequentialStopwatch.ElapsedMilliseconds} ms; parallel: {parallelStopwatch.ElapsedMilliseconds} ms; speed-up: {speedup:F2}x");
            Console.WriteLine($"Distinct parallel threads: {threadIds.Count}; processor count: {Environment.ProcessorCount}");
        }

        private async Task RunEmailResendAsync(CancellationToken ct)
        {
            List<Customer> customers = await _store.ReadAsync<List<Customer>>(DataFiles.Customers, ct);
            Customer customer = customers.FirstOrDefault() ?? throw new InvalidOperationException("No customer is available for resend emails.");
            List<Order> testOrders = Enumerable.Range(1001, 30).Select(id => new Order
            {
                Id = id,
                CustomerId = customer.Id,
                TotalAmount = 0m,
                Status = OrderStatus.Completed
            }).ToList();
            IEmailService emailService = new EmailService(_store);
            int running = 0;
            int highestRunning = 0;
            var stopwatch = Stopwatch.StartNew();

            await Parallel.ForEachAsync(testOrders, new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = 4
            }, async (order, token) =>
            {
                int nowRunning = Interlocked.Increment(ref running);
                UpdateMaximum(ref highestRunning, nowRunning);
                try
                {
                    await emailService.SendConfirmationAsync(order, customer, token);
                }
                finally
                {
                    Interlocked.Decrement(ref running);
                }
            });
            stopwatch.Stop();

            List<EmailMessage> emails = await _store.ReadAsync<List<EmailMessage>>(DataFiles.Emails, ct);
            int[] expectedIds = testOrders.Select(order => order.Id).ToArray();
            int[] resendCounts = emails.Where(email => expectedIds.Contains(email.OrderId))
                .GroupBy(email => email.OrderId)
                .Select(group => group.Count())
                .ToArray();
            if (resendCounts.Length != 30 || resendCounts.Any(count => count != 1))
                throw new InvalidOperationException("The resend run did not save each test email exactly once.");
            if (highestRunning != 4)
                throw new InvalidOperationException($"Expected exactly four concurrent email sends, observed {highestRunning}.");

            Console.WriteLine($"Sent 30 test emails in {stopwatch.Elapsed.TotalSeconds:F1} s; highest concurrent sends: {highestRunning}");
        }

        private static int Score(int orderId)
        {
            long h = orderId;
            for (int i = 0; i < 100_000; i++)
                h = (h * 31 + i) % 1_000_000_007;
            return (int)(h % 100);
        }

        private static void UpdateMaximum(ref int maximum, int candidate)
        {
            int current;
            while (candidate > (current = Volatile.Read(ref maximum)) &&
                   Interlocked.CompareExchange(ref maximum, candidate, current) != current) { }
        }
    }
}
