using OrderSystem.Logs;

namespace OrderSystem.Repositories
{
    internal static class RepositoryLatency
    {
        public static async Task QueryAsync(string repository, string query, CancellationToken ct)
        {
            Log.Info($"{repository} -> Opening connection...");
            await Task.Delay(200, ct);

            int queryDelay = Random.Shared.Next(300, 600 + 1);
            Log.Info($"{repository} -> Executing {query}...");
            await Task.Delay(queryDelay, ct);
        }

        public static async Task BeginWriteAsync(string repository, string operation, CancellationToken ct)
        {
            Log.Info($"{repository} -> Opening connection...");
            await Task.Delay(200, ct);

            Log.Info($"{repository} -> Beginning transaction...");
            await Task.Delay(100, ct);

            Log.Info($"{repository} -> {operation}...");
            await Task.Delay(400, ct);
        }

        public static async Task CommitAsync(string repository, CancellationToken ct)
        {
            Log.Info($"{repository} -> Committing transaction...");
            await Task.Delay(200, ct);
        }
    }
}
