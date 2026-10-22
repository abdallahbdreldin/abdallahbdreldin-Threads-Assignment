using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class SyncCustomerRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public SyncCustomerRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public Customer? GetById(int id, CancellationToken ct)
        {
            SyncRepositoryLatency.Query(nameof(SyncCustomerRepository), $"query for Customer #{id}", ct);

            List<Customer> customers = _jsonFileStore.Read<List<Customer>>(DataFiles.Customers);

            Customer? customer = customers.FirstOrDefault(candidate => candidate.Id == id);

            if (customer is null)
                Log.Error($"{nameof(SyncCustomerRepository)} -> Customer #{id} not found");
            else
                Log.Success($"{nameof(SyncCustomerRepository)} -> Customer #{id} found ({customer.Name})");

            return customer;
        }
    }
}
