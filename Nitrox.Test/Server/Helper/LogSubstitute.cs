using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;

namespace Nitrox.Test.Server.Helper;

/// <summary>
///     Substituted loggers for tests of what is logged, and of what happens when logging fails.
/// </summary>
internal static class LogSubstitute
{
    /// <summary>
    ///     A logger that takes every level and adds each line it is given to <paramref name="entries" />. The line is
    ///     formatted while the call runs: what ZLogger passes as the state goes back to its pool right after the call,
    ///     so it cannot be read from the substitute's received calls later.
    /// </summary>
    public static ILogger<T> Capturing<T>(List<(LogLevel Level, string Message)> entries)
    {
        ILogger<T> logger = Substitute.For<ILogger<T>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        WhenLogging(logger).Do(call =>
        {
            string message = (string)((Delegate)call[4]).DynamicInvoke(call[2], call[3])!;
            lock (entries)
            {
                entries.Add(((LogLevel)call[0], message));
            }
        });
        return logger;
    }

    /// <summary>
    ///     A logger whose sink is gone: every line that is written to it throws.
    /// </summary>
    public static ILogger<T> Throwing<T>()
    {
        ILogger<T> logger = Substitute.For<ILogger<T>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        WhenLogging(logger).Do(_ => throw new InvalidOperationException("log sink is gone"));
        return logger;
    }

    private static WhenCalled<ILogger<T>> WhenLogging<T>(ILogger<T> logger) =>
        logger.When(log => log.Log(Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<Arg.AnyType>(), Arg.Any<Exception?>(), Arg.Any<Func<Arg.AnyType, Exception?, string>>()));
}
