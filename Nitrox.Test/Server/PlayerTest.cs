using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
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

    [TestMethod]
    public void APlayerInAMovingCyclopsIsWhereTheCyclopsTookThem()
    {
        ServerPlayer player = new ConnectedPlayers().Connect("Bob");
        NitroxTransform cyclops = new(new NitroxVector3(100, -20, 100), NitroxQuaternion.Identity, NitroxVector3.One);
        player.Entity = new PlayerEntity(new NitroxTransform(new NitroxVector3(100, -20, 105), NitroxQuaternion.Identity, NitroxVector3.One), 0, null, false, player.GameObjectId,
                                         NitroxTechType.None, null, null, []);
        player.Entity.Transform.SetParent(cyclops, true);
        player.Position = new NitroxVector3(100, -20, 105);

        cyclops.LocalPosition = new NitroxVector3(400, -20, 100);

        player.CurrentPosition.Should().Be(new NitroxVector3(400, -20, 105));
    }

    [TestMethod]
    public void APlayerOutsideIsWhereTheyLastMoved()
    {
        ServerPlayer player = new ConnectedPlayers().Connect("Bob");
        player.Position = new NitroxVector3(1, 2, 3);

        player.CurrentPosition.Should().Be(new NitroxVector3(1, 2, 3));
    }
}
