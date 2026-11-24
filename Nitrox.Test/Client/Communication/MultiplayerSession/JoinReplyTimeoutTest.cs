using NitroxClient.Communication.MultiplayerSession;

namespace Nitrox.Test.Client.Communication.MultiplayerSession;

[TestClass]
public sealed class JoinReplyTimeoutTest
{
    [TestMethod]
    public void TheGameStopsWaitingForAServerThatDoesNotAnswer()
    {
        JoinReplyTimeout.For(MultiplayerSessionConnectionStage.ESTABLISHING_SERVER_POLICY).Should().NotBeNull();
        JoinReplyTimeout.For(MultiplayerSessionConnectionStage.AWAITING_SESSION_RESERVATION).Should().NotBeNull();
    }

    [TestMethod]
    public void ThePlayerIsNeverHurriedWhileTypingANameOrPassword()
    {
        JoinReplyTimeout.For(MultiplayerSessionConnectionStage.AWAITING_RESERVATION_CREDENTIALS).Should().BeNull();
        JoinReplyTimeout.For(MultiplayerSessionConnectionStage.SESSION_RESERVED).Should().BeNull();
        JoinReplyTimeout.For(MultiplayerSessionConnectionStage.SESSION_JOINED).Should().BeNull();
    }
}
