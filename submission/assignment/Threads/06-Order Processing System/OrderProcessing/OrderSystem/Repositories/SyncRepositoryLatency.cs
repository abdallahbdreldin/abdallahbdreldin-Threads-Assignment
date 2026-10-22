using OrderSystem.Logs;

namespace OrderSystem.Repositories
{
    internal static class SyncRepositoryLatency
    {
        public static void Query(string repository, string query, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            Log.Info($"{repository} -> Opening connection...");

            Thread.Sleep(200);
            ct.ThrowIfCancellationRequested();

            int queryDelay = Random.Shared.Next(300, 600 + 1);

            Log.Info($"{repository} -> Executing {query}...");

            Thread.Sleep(queryDelay);
            ct.ThrowIfCancellationRequested();
        }
    }
}
