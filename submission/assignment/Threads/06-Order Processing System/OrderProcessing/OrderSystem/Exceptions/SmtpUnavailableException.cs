namespace OrderSystem.Exceptions
{
    public sealed class SmtpUnavailableException : Exception
    {
        public SmtpUnavailableException(string message) : base(message)
        {
        }
    }
}
