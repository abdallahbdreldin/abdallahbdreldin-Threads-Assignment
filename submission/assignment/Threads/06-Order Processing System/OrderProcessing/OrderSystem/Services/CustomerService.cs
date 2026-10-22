using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public class CustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        }

        public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct)
        {
            Customer? customer = await _customerRepository.GetByIdAsync(id, ct);

            if (customer is null)
                throw new CustomerNotFoundException($"Customer #{id} not found");

            return customer;
        }
    }
}
