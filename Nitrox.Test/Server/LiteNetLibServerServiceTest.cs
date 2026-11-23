using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server;

/// <summary>
///     Who a broadcast reaches: players only, as a session in the main menu or the join queue has no multiplayer world.
/// </summary>
[TestClass]
public sealed class LiteNetLibServerServiceTest
{
    [TestMethod]
    public void WorldBroadcastsReachPlayersOnly()
    {
        LiteNetLibServerService.ReceivesBroadcast(new EntityDestroyed(new NitroxId()), true).Should().BeTrue();
        LiteNetLibServerService.ReceivesBroadcast(new EntityDestroyed(new NitroxId()), false).Should().BeFalse();
    }

    [TestMethod]
    public void EveryoneHearsThatTheServerStops()
    {
        LiteNetLibServerService.ReceivesBroadcast(new ServerStopped(), false).Should().BeTrue();
    }
}
