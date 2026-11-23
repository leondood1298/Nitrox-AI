using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server;

[TestClass]
public sealed class PlayerTest
{
    [TestMethod]
    public void TeleportingRemembersTheSubThePlayerLeftForBack()
    {
        ServerPlayer player = new ConnectedPlayers().Connect("Bob");
        NitroxId homeBase = new();
        NitroxId otherBase = new();
        player.SubRootId = Optional.Of(homeBase);
        player.Position = new NitroxVector3(1, 2, 3);

        player.Teleport(new NitroxVector3(10, 20, 30), Optional.Of(otherBase), Substitute.For<IPacketSender>());

        player.LastStoredPosition.Should().Be(new NitroxVector3(1, 2, 3));
        player.LastStoredSubRootID.Value.Should().Be(homeBase);
        player.Position.Should().Be(new NitroxVector3(10, 20, 30));
    }
}
