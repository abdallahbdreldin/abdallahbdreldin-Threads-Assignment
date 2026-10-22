using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Models;
using OrderSystem.Repositories;

namespace OrderSystem.Services
{
    public sealed class CustomerDashboardService
    {
        private readonly IJsonFileStore _store;
        private readonly HashSet<int> _failPaymentsForCustomerIds;

        public CustomerDashboardService(IJsonFileStore store, IEnumerable<int>? failPaymentsForCustomerIds = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _failPaymentsForCustomerIds = failPaymentsForCustomerIds?.ToHashSet() ?? new HashSet<int>();
        }

        public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct)
        {
            Task<Customer> customerTask = LoadCustomerAsync(customerId, ct);
            Task<List<Order>> ordersTask = LoadOrdersAsync(customerId, ct);
            Task<List<PaymentTransaction>> paymentsTask = LoadPaymentsAsync(customerId, ct);
            Task<List<EmailMessage>> emailsTask = LoadEmailsAsync(customerId, ct);
            Task[] tasks = { customerTask, ordersTask, paymentsTask, emailsTask };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                Exception[] failures = tasks
                    .Where(task => task.IsFaulted)
                    .SelectMany(task => task.Exception!.Flatten().InnerExceptions)
                    .ToArray();

                throw new AggregateException(failures);
            }

            return new CustomerDashboard
            {
                Customer = await customerTask,
                Orders = await ordersTask,
                Payments = await paymentsTask,
                Emails = await emailsTask
            };
        }

        private async Task<Customer> LoadCustomerAsync(int customerId, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(CustomerDashboardService), $"profile for Customer #{customerId}", ct);
            List<Customer> customers = await _store.ReadAsync<List<Customer>>(DataFiles.Customers, ct);
            return customers.FirstOrDefault(customer => customer.Id == customerId)
                ?? throw new CustomerNotFoundException($"Customer #{customerId} not found");
        }

        private async Task<List<Order>> LoadOrdersAsync(int customerId, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(CustomerDashboardService), $"orders for Customer #{customerId}", ct);
            List<Order> orders = await _store.ReadAsync<List<Order>>(DataFiles.Orders, ct);
            return orders.Where(order => order.CustomerId == customerId).OrderBy(order => order.Id).ToList();
        }

        private async Task<List<PaymentTransaction>> LoadPaymentsAsync(int customerId, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(CustomerDashboardService), $"payments for Customer #{customerId}", ct);
            if (_failPaymentsForCustomerIds.Contains(customerId))
                throw new TimeoutException("Payments store timeout");

            List<Order> orders = await _store.ReadAsync<List<Order>>(DataFiles.Orders, ct);
            HashSet<int> orderIds = orders.Where(order => order.CustomerId == customerId).Select(order => order.Id).ToHashSet();
            List<PaymentTransaction> payments = await _store.ReadAsync<List<PaymentTransaction>>(DataFiles.Payments, ct);
            return payments.Where(payment => orderIds.Contains(payment.OrderId)).OrderBy(payment => payment.OrderId).ToList();
        }

        private async Task<List<EmailMessage>> LoadEmailsAsync(int customerId, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(CustomerDashboardService), $"emails for Customer #{customerId}", ct);
            List<Order> orders = await _store.ReadAsync<List<Order>>(DataFiles.Orders, ct);
            HashSet<int> orderIds = orders.Where(order => order.CustomerId == customerId).Select(order => order.Id).ToHashSet();
            List<EmailMessage> emails = await _store.ReadAsync<List<EmailMessage>>(DataFiles.Emails, ct);
            return emails.Where(email => orderIds.Contains(email.OrderId)).OrderBy(email => email.OrderId).ToList();
        }
    }
}
