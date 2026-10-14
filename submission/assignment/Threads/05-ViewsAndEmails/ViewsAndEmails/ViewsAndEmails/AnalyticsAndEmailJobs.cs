namespace ViewsAndEmails;

public sealed class AnalyticsAndEmailJobs
{
    public readonly Dictionary<string, int> Views = new();
    public readonly Queue<string> EmailJobs = new();
    public readonly List<string> SentEmails = new();

    readonly object _sentSync = new();
    public volatile bool ProducersDone;

    public void RecordView(string product)
    {
        if (!Views.ContainsKey(product))
            Views.Add(product, 0);

        Views[product] = Views[product] + 1;
    }

    public void PlaceOrder(int orderId)
    {
        EmailJobs.Enqueue($"Email: order {orderId}");
    }

    public void EmailWorker()
    {
        while (true)
        {
            try
            {
                string? job = null;
                if (EmailJobs.Count > 0)
                    job = EmailJobs.Dequeue();

                if (job is not null)
                {
                    lock (_sentSync)
                        SentEmails.Add(job);
                    continue;
                }

                if (ProducersDone && EmailJobs.Count == 0)
                    return;

                Thread.Sleep(1);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("InvalidOperationException");
                if (ProducersDone)
                    return;
            }
        }
    }
}
