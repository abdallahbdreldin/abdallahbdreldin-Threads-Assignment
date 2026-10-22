namespace OrderSystem.Exceptions
{
    public sealed class InvalidCouponException : Exception
    {
        public InvalidCouponException(string message) : base(message)
        {
        }
    }
}
