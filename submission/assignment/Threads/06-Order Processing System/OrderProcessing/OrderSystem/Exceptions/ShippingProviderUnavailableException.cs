namespace OrderSystem.Exceptions
{
    public sealed class ShippingProviderUnavailableException : Exception
    {
        public ShippingProviderUnavailableException(string message) : base(message)
        {
        }
    }
}
