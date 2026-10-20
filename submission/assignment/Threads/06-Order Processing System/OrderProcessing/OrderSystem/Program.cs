using OrderSystem.Runners;

namespace OrderSystem
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (!TryReadCancelAfter(args, out int? cancelAfterMs))
            {
                Console.Error.WriteLine("Usage: dotnet run -- [phase1] [--cancel-after <ms>]");
                return 1;
            }

            if (HasUnsupportedArguments(args))
            {
                Console.Error.WriteLine("Only phase1 is implemented in this project state.");
                return 1;
            }

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            if (cancelAfterMs.HasValue)
                cts.CancelAfter(cancelAfterMs.Value);

            try
            {
                var runner = new Phase1Runner(new JsonFileStore());
                await runner.RunAsync(cts.Token);
                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static bool TryReadCancelAfter(string[] args, out int? cancelAfterMs)
        {
            cancelAfterMs = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != "--cancel-after") continue;
                if (cancelAfterMs.HasValue || i + 1 >= args.Length ||
                    !int.TryParse(args[++i], out int milliseconds) || milliseconds < 0)
                    return false;

                cancelAfterMs = milliseconds;
            }

            return true;
        }

        private static bool HasUnsupportedArguments(string[] args)
        {
            bool phaseWasSpecified = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "phase1" && !phaseWasSpecified)
                {
                    phaseWasSpecified = true;
                    continue;
                }

                if (args[i] == "--cancel-after")
                {
                    i++;
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
