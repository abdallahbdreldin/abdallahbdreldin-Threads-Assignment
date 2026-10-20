namespace OrderSystem.Models
{
    public enum PaymentStatus { Captured, Declined, Refunded }
    public sealed class PaymentTransaction
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
