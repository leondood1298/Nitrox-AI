using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A death in hardcore takes the player out of this world for good, with what they carried; any other death leaves a
///     beacon the player keeps until they reach it.
/// </summary>
[TestClass]
public sealed class PlayerDeathsTest
{
    private static readonly NitroxVector3 deathPosition = new(100, -50, 20);

    [TestMethod]
    public void AHardcoreDeathRemovesThePlayerAndWhatTheyCarried()
    {
        Scenario scenario = new(SubnauticaGameMode.HARDCORE);
        NitroxId knife = scenario.GiveItem();

        bool permanent = scenario.Deaths.Died(scenario.Player, deathPosition);

        permanent.Should().BeTrue();
        scenario.Player.IsPermaDeath.Should().BeTrue();
        scenario.Registry.TryGetEntityById(scenario.Player.GameObjectId, out Entity _).Should().BeFalse("the body is not left in the save");
        scenario.Registry.TryGetEntityById(knife, out Entity _).Should().BeFalse("nothing they carried is left where nobody can reach it");
        scenario.Player.EquippedItems.Should().BeEmpty();
    }

    [TestMethod]
    public void ADeadHardcorePlayerCannotJoinAgain()
    {
        Scenario scenario = new(SubnauticaGameMode.HARDCORE);
        scenario.Deaths.Died(scenario.Player, deathPosition);
        scenario.Players.Disconnect(scenario.Player.SessionId);

        scenario.Players.Reserve("Diver", "key", out MultiplayerSessionReservation again);

        again.ReservationState.HasStateFlag(MultiplayerSessionReservationState.HARDCORE_PLAYER_DEAD).Should().BeTrue();
    }

    [TestMethod]
    public void ASurvivalDeathKeepsThePlayerAndLeavesABeacon()
    {
        Scenario scenario = new(SubnauticaGameMode.SURVIVAL, markDeaths: true);
        NitroxId knife = scenario.GiveItem();

        bool permanent = scenario.Deaths.Died(scenario.Player, deathPosition);

        permanent.Should().BeFalse();
        scenario.Player.IsPermaDeath.Should().BeFalse();
        scenario.Registry.TryGetEntityById(knife, out Entity _).Should().BeTrue();
        scenario.Player.DeathMarkers.Should().Equal(deathPosition);
        scenario.Player.LastStoredPosition.Should().Be(deathPosition);
    }

    [TestMethod]
    public void NoBeaconWhenDeathMarkersAreOff()
    {
        Scenario scenario = new(SubnauticaGameMode.SURVIVAL, markDeaths: false);

        scenario.Deaths.Died(scenario.Player, deathPosition);

        scenario.Player.DeathMarkers.Should().BeEmpty();
    }

    [TestMethod]
    public void AReachedBeaconIsForgotten()
    {
        Scenario scenario = new(SubnauticaGameMode.SURVIVAL, markDeaths: true);
        NitroxVector3 other = new(500, -10, 500);
        scenario.Deaths.Died(scenario.Player, deathPosition);
        scenario.Deaths.Died(scenario.Player, other);

        PlayerDeaths.ForgetMarker(scenario.Player, new NitroxVector3(101, -50, 20));

        scenario.Player.DeathMarkers.Should().Equal(other);
    }

    private sealed class Scenario
    {
        public ConnectedPlayers Players { get; } = new();
        public EntityRegistry Registry { get; } = new(NullLogger<EntityRegistry>.Instance);
        public ServerPlayer Player { get; }
        public PlayerDeaths Deaths { get; }

        public Scenario(SubnauticaGameMode gameMode, bool markDeaths = false)
        {
            Player = Players.Connect("Diver");
            Player.GameMode = gameMode;
            WorldEntityManager world = new(Substitute.For<IPacketSender>(), Registry, null!, Players.PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
            PlayerEntity body = new(new NitroxTransform(deathPosition, NitroxQuaternion.Identity, NitroxVector3.One), 0, null, false, Player.GameObjectId, NitroxTechType.None, null, null, []);
            Registry.AddOrUpdate(body);
            world.TrackEntityInTheWorld(body);
            Player.Entity = body;
            Deaths = new PlayerDeaths(world, new PlayerHandAndSeatStates(), Options.Create(new SubnauticaServerOptions { MarkDeathPointsWithBeacon = markDeaths }), NullLogger<PlayerDeaths>.Instance);
        }

        public NitroxId GiveItem()
        {
            NitroxId id = new();
            InventoryItemEntity item = new(id, "knife", new NitroxTechType("Knife"), null, Player.GameObjectId, []);
            Registry.AddOrUpdate(item);
            Player.EquippedItems["Hand"] = id;
            return id;
        }
    }
}
