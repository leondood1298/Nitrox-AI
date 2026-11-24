using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Communication;
using static Nitrox.Server.Subnautica.Models.Communication.PacketReadGate;

namespace Nitrox.Test.Server.Communication;

[TestClass]
public sealed class PacketReadGateTest
{
    [TestMethod]
    public void ASessionThatHasNotJoinedMaySendTheSessionPackets()
    {
        Decide(typeof(MultiplayerSessionReservationRequest), 300, false, true, DEFAULT_MAX_BYTES).Should().Be(Verdict.READ);
    }

    [TestMethod]
    public void ASessionThatHasNotJoinedSendingALargeSessionPacketIsRefused()
    {
        Decide(typeof(MultiplayerSessionReservationRequest), NOT_JOINED_MAX_BYTES + 1, false, true, DEFAULT_MAX_BYTES).Should().Be(Verdict.REFUSE);
    }

    [TestMethod]
    public void APlayersPacketFromASessionThatHasNotJoinedIsLeftUnread()
    {
        Decide(typeof(EntitySpawnedByClient), 300, false, false, DEFAULT_MAX_BYTES).Should().Be(Verdict.SKIP);
    }

    [TestMethod]
    public void DataNamingNoPacketTypeIsRefusedFromAnyone()
    {
        Decide(null, 10, false, false, 0).Should().Be(Verdict.REFUSE);
        Decide(null, 10, true, false, 0).Should().Be(Verdict.REFUSE);
    }

    [TestMethod]
    public void APlayersPacketWithinItsTypesSizeIsRead()
    {
        Decide(typeof(ChatMessage), DEFAULT_MAX_BYTES, true, false, DEFAULT_MAX_BYTES).Should().Be(Verdict.READ);
    }

    [TestMethod]
    public void APlayersPacketLargerThanItsTypeMayBeIsLeftUnread()
    {
        Decide(typeof(ChatMessage), DEFAULT_MAX_BYTES + 1, true, false, DEFAULT_MAX_BYTES).Should().Be(Verdict.SKIP);
    }
}
