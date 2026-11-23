using Nitrox.Model.Constants;

namespace Nitrox.Test.Model.Constants;

[TestClass]
public sealed class ServerExitCodesTest
{
    [TestMethod]
    public void NormalStopIsPlain()
    {
        ServerExitCodes.Describe(0).Should().Be("Server stopped.");
        ServerExitCodes.Describe(null).Should().Be("Server stopped.");
    }

    [TestMethod]
    public void KnownFailuresSayWhy()
    {
        ServerExitCodes.Describe(ServerExitCodes.WORLD_LOAD_FAILED).Should().Contain("world cannot be opened");
        ServerExitCodes.Describe(ServerExitCodes.PORT_UNAVAILABLE).Should().Contain("port");
        ServerExitCodes.Describe(ServerExitCodes.WORLD_IN_USE).Should().Contain("already open");
    }

    [TestMethod]
    public void CrashPointsToTheLog()
    {
        ServerExitCodes.Describe(-532462766).Should().Contain("unexpectedly").And.Contain("log");
    }
}
