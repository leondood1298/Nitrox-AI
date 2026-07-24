namespace ScannerRoom.NetworkImpairmentProxy;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine(CommandLine.USAGE);
            return 0;
        }
        if (args.Length == 1 && args[0] == "--self-test")
        {
            try
            {
                await SelfTests.RunAsync();
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"[SELFTEST] FAIL type={exception.GetType().Name} message={exception.Message}");
                return 1;
            }
        }
        if (args.Length == 0)
        {
            Console.Error.WriteLine(CommandLine.USAGE);
            return 2;
        }

        try
        {
            ProxyOptions options = CommandLine.Parse(args);
            using CancellationTokenSource stopping = new();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                stopping.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            try
            {
                await using UdpImpairmentProxy proxy = new(options);
                await proxy.RunAsync(stopping.Token);
                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"[NIP1] ev=config-error message={exception.Message}");
            Console.Error.WriteLine(CommandLine.USAGE);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[NIP1] ev=fatal type={exception.GetType().Name} message={exception.Message}");
            return 1;
        }
    }
}
