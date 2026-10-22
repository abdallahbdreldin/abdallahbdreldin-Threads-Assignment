using OrderSystem.Models;

namespace OrderSystem.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct);
    }
}
