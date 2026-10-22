namespace OrderSystem.Models
{
    public sealed class CustomerDashboard
    {
        public required Customer Customer { get; init; }
        public List<Order> Orders { get; init; } = new();
        public List<PaymentTransaction> Payments { get; init; } = new();
        public List<EmailMessage> Emails { get; init; } = new();
    }
}
