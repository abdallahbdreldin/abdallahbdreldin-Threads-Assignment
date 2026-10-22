using System.Diagnostics;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class ShippingQuoteService
    {
        private readonly IReadOnlyList<IShippingCarrier> _carriers;
        private readonly TimeSpan _timeout;

        public ShippingQuoteService(IEnumerable<IShippingCarrier> carriers, TimeSpan? timeout = null)
        {
            _carriers = carriers?.ToList() ?? throw new ArgumentNullException(nameof(carriers));
            if (_carriers.Count != 3)
                throw new ArgumentException("Exactly three shipping carriers are required.", nameof(carriers));
            _timeout = timeout ?? TimeSpan.FromMilliseconds(1200);
        }

        public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct)
        {
            using var carrierCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = _carriers.Select(carrier =>
            {
                var stopwatch = Stopwatch.StartNew();
                return (Carrier: carrier, Stopwatch: stopwatch, Task: carrier.GetQuoteAsync(order, carrierCts.Token));
            }).ToList();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task timeoutTask = Task.Delay(_timeout, timeoutCts.Token);
            try
            {
                while (pending.Count > 0)
                {
                    var winner = await Task.WhenAny(pending.Select(entry => (Task)entry.Task).Append(timeoutTask));
                    if (winner == timeoutTask)
                    {
                        ct.ThrowIfCancellationRequested();
                        carrierCts.Cancel();
                        await ObserveCancelledAsync(pending.Select(entry => (Task)entry.Task));
                        return new ShippingQuote("Flat rate", 25.00m, (long)_timeout.TotalMilliseconds);
                    }

                    int index = pending.FindIndex(entry => ReferenceEquals(entry.Task, winner));
                    var completed = pending[index];
                    pending.RemoveAt(index);

                    try
                    {
                        decimal price = await completed.Task;
                        completed.Stopwatch.Stop();
                        carrierCts.Cancel();
                        await ObserveCancelledAsync(pending.Select(entry => (Task)entry.Task));
                        return new ShippingQuote(completed.Carrier.Name, price, completed.Stopwatch.ElapsedMilliseconds);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        completed.Stopwatch.Stop();
                        Log.Error($"[{completed.Carrier.Name}] quote failed: {exception.Message}; trying remaining carriers");
                    }
                }

                carrierCts.Cancel();
                return new ShippingQuote("Flat rate", 25.00m, (long)_timeout.TotalMilliseconds);
            }
            finally
            {
                timeoutCts.Cancel();
            }
        }

        private static async Task ObserveCancelledAsync(IEnumerable<Task> tasks)
        {
            foreach (Task task in tasks)
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (Exception exception) { Log.Error($"Shipping carrier ended with: {exception.Message}"); }
            }
        }
    }
}
