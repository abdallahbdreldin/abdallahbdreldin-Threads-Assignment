using OrderSystem.Runners;

namespace OrderSystem
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (!TryReadCancelAfter(args, out int? cancelAfterMs))
            {
                Console.Error.WriteLine("Usage: dotnet run -- [phase1|phase2|phase3|phase4] [--cancel-after <ms>]");
                return 1;
            }

            if (HasUnsupportedArguments(args))
            {
                Console.Error.WriteLine("Only phase1, phase2, phase3 and phase4 are implemented in this project state.");
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
                var jsonFileStore = new JsonFileStore();
                string phase = args.FirstOrDefault(argument => argument is "phase1" or "phase2" or "phase3" or "phase4") ?? "phase1";

                if (phase == "phase2")
                {
                    var runner = new Phase2Runner(jsonFileStore);
                    await runner.RunAsync(cts.Token);
                }
                else if (phase == "phase3")
                {
                    var runner = new Phase3Runner(jsonFileStore);
                    await runner.RunAsync(cts.Token);
                }
                else if (phase == "phase4")
                {
                    var runner = new Phase4Runner(jsonFileStore);
                    await runner.RunAsync(cts.Token);
                }
                else
                {
                    var runner = new Phase1Runner(jsonFileStore);
                    await runner.RunAsync(cts.Token);
                }
                return 0;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                Console.WriteLine("Run cancelled.");
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
                if ((args[i] is "phase1" or "phase2" or "phase3" or "phase4") && !phaseWasSpecified)
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
