namespace OrderSystem.Models
{
    public sealed class Coupon
    {
        public string Code { get; set; } = string.Empty;
        public int DiscountPercent { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}
