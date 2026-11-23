using Microsoft.Extensions.Logging;

namespace Nitrox.Test.Server.Helper;

/// <summary>
///     A logger that is enabled but keeps nothing. NullLogger reports every level as disabled, and the "log once"
///     methods (ZLogWarningOnce and the like) throw when called on a disabled logger.
/// </summary>
internal sealed class DiscardingLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
