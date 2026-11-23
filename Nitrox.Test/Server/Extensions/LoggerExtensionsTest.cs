using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Extensions;

namespace Nitrox.Test.Server.Extensions;

[TestClass]
public class LoggerExtensionsTest
{
    [TestMethod]
    public void LogOnceDoesNotThrowWhenItsLevelIsSwitchedOff()
    {
        // NullLogger reports every level as disabled, like a real logger whose minimum level is above the message's
        ILogger logger = NullLogger.Instance;
        int value = 42;

        Action logWarning = () => logger.ZLogWarningOnce($"Something happened with {value}");
        Action logError = () => logger.ZLogErrorOnce($"Something failed with {value}");

        logWarning.Should().NotThrow();
        logError.Should().NotThrow();
    }
}
