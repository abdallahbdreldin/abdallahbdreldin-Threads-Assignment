using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Repositories
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly IJsonFileStore _jsonFileStore;

        public CustomerRepository(IJsonFileStore jsonFileStore)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
        }

        public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct)
        {
            await RepositoryLatency.QueryAsync(nameof(CustomerRepository), $"query for Customer #{id}", ct);

            List<Customer> customers = await _jsonFileStore.ReadAsync<List<Customer>>(DataFiles.Customers, ct);
            Customer? customer = customers.FirstOrDefault(candidate => candidate.Id == id);

            if (customer is null)
                Log.Error($"{nameof(CustomerRepository)} -> Customer #{id} not found");
            else
                Log.Success($"{nameof(CustomerRepository)} -> Customer #{id} found ({customer.Name})");

            return customer;
        }
    }
}
