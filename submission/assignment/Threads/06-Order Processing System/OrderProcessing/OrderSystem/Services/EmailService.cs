using OrderSystem.Exceptions;
using OrderSystem.Interfaces.IJsonFile;
using OrderSystem.Interfaces.Services;
using OrderSystem.Logs;
using OrderSystem.Models;

namespace OrderSystem.Services
{
    public sealed class EmailService : IEmailService
    {
        private const int MaxAttempts = 4;
        private readonly IJsonFileStore _jsonFileStore;
        private readonly IReadOnlyDictionary<int, int> _failAttempts;

        public EmailService(IJsonFileStore jsonFileStore, IReadOnlyDictionary<int, int>? failAttempts = null)
        {
            _jsonFileStore = jsonFileStore ?? throw new ArgumentNullException(nameof(jsonFileStore));
            _failAttempts = failAttempts is null
                ? new Dictionary<int, int>()
                : new Dictionary<int, int>(failAttempts);
        }

        public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(customer);

            var email = new EmailMessage
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                To = customer.Email,
                Subject = $"Order #{order.Id} confirmed",
                Body = $"Your order #{order.Id} is complete. Total: {order.TotalAmount:F2}.",
                SentAt = DateTime.Now
            };

            string? lastError = null;
            int configuredFailures = _failAttempts.GetValueOrDefault(order.Id);

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await SendAttemptAsync(order.Id, attempt, configuredFailures, ct);

                    email.Attempts = attempt;
                    email.LastError = null;
                    email.SentAt = DateTime.Now;
                    await _jsonFileStore.UpdateAsync<List<EmailMessage>>(DataFiles.Emails, emails =>
                    {
                        emails.Add(email);
                        return emails;
                    }, ct);
                    Log.Success($"{nameof(EmailService)} -> Confirmation for Order #{order.Id} sent on attempt {attempt}");
                    return;
                }
                catch (SmtpUnavailableException exception)
                {
                    lastError = exception.Message;
                    Log.Error($"{nameof(EmailService)} -> Order #{order.Id} email attempt {attempt} failed: {lastError}");

                    if (attempt < MaxAttempts)
                        await Task.Delay(500 * (1 << (attempt - 1)), ct);
                }
            }

            email.Attempts = MaxAttempts;
            email.LastError = lastError;
            email.SentAt = DateTime.Now;
            await _jsonFileStore.UpdateAsync<List<EmailMessage>>(DataFiles.EmailRetries, retries =>
            {
                retries.Add(email);
                return retries;
            }, ct);
            Log.Error($"Email for Order #{order.Id} queued for retry later");
        }

        private static async Task SendAttemptAsync(int orderId, int attempt, int configuredFailures, CancellationToken ct)
        {
            Log.Info($"{nameof(EmailService)} -> Order #{orderId} rendering email template (attempt {attempt})...");
            await Task.Delay(200, ct);

            Log.Info($"{nameof(EmailService)} -> Order #{orderId} connecting to SMTP...");
            await Task.Delay(300, ct);

            Log.Info($"{nameof(EmailService)} -> Order #{orderId} sending email...");
            await Task.Delay(500, ct);

            if (attempt <= configuredFailures)
                throw new SmtpUnavailableException("SMTP server unavailable");
        }
    }
}
