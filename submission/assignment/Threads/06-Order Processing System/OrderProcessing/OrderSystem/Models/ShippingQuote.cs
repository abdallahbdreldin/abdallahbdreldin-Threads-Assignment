namespace OrderSystem.Models
{
    public sealed record ShippingQuote(string Carrier, decimal Price, long ElapsedMs);
}
