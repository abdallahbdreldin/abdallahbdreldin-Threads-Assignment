using OrderSystem.Models;

namespace OrderSystem.Interfaces.Services
{
    public interface IShippingCarrier
    {
        string Name { get; }
        Task<decimal> GetQuoteAsync(Order order, CancellationToken ct);
    }
}
