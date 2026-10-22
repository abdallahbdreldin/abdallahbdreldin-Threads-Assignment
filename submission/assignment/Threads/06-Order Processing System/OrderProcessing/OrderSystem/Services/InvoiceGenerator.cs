using System.Text;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class InvoiceGenerator
    {
        private const int RenderIterations = 20_000_000;
        private readonly string _dataDirectory;

        public InvoiceGenerator(string dataDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
            _dataDirectory = dataDirectory;
        }

        public async Task GenerateAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);

            long renderChecksum = await Task.Run(() => RenderInvoice(order, ct), ct);

            string invoiceDirectory = Path.Combine(_dataDirectory, "invoices");
            Directory.CreateDirectory(invoiceDirectory);

            string invoicePath = Path.Combine(invoiceDirectory, $"invoice_{order.Id}.txt");
            string invoice =
                $"Invoice for Order #{order.Id}{Environment.NewLine}" +
                $"Customer: {order.CustomerId}{Environment.NewLine}" +
                $"Subtotal: {order.SubTotal:F2}{Environment.NewLine}" +
                $"Discount: {order.Discount:F2}{Environment.NewLine}" +
                $"Shipping: {order.ShippingCost:F2}{Environment.NewLine}" +
                $"Total: {order.TotalAmount:F2}{Environment.NewLine}" +
                $"Render checksum: {renderChecksum}{Environment.NewLine}";

            await File.WriteAllTextAsync(invoicePath, invoice, Encoding.UTF8, ct);
        }

        private static long RenderInvoice(Order order, CancellationToken ct)
        {
            long checksum = order.Id;
            for (int i = 0; i < RenderIterations; i++)
            {
                if (i % 100_000 == 0)
                    ct.ThrowIfCancellationRequested();

                checksum = unchecked((checksum * 31) + i);
            }

            return checksum;
        }
    }
}
