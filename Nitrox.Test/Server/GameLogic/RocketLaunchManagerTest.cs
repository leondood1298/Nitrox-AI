using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The rocket launches only with every player in the world aboard, and then for all of them. Otherwise the player who
///     clicked learns who is missing and nothing else happens.
/// </summary>
[TestClass]
public sealed class RocketLaunchManagerTest
{
    private static readonly NitroxVector3 rocketPosition = new(300f, 0f, -1200f);
    private static readonly NitroxVector3 cockpitCenter = new(300f, 35f, -1200f);
    private static readonly NitroxVector3 inCockpit = cockpitCenter + new NitroxVector3(1f, -1f, 0.5f);
    private static readonly NitroxVector3 onPlatformDeck = new(310f, 2f, -1200f);

    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private IPacketSender packetSender;
    private RocketLaunchManager rocketLaunchManager;
    private NitroxId rocketId;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        packetSender = Substitute.For<IPacketSender>();
        rocketLaunchManager = new RocketLaunchManager(players.PlayerManager, entityRegistry, packetSender, NullLogger<RocketLaunchManager>.Instance);
        rocketId = AddEntity("RocketBase", rocketPosition);
    }

    [TestMethod]
    public async Task EverybodyAboardLaunchesForEverybody()
    {
        Player first = InWorld("First", inCockpit);
        InWorld("Second", cockpitCenter);

        await RequestLaunch(first);

        ConnectedPlayers.SentToAll<RocketLaunch>(packetSender).Should().ContainSingle().Which.RocketId.Should().Be(rocketId);
        ConnectedPlayers.SentTo<RocketLaunchRefused>(packetSender, first).Should().BeEmpty();
    }

    [TestMethod]
    public async Task APlayerAloneLaunchesAsBefore()
    {
        Player alone = InWorld("Alone", inCockpit);

        await RequestLaunch(alone);

        ConnectedPlayers.SentToAll<RocketLaunch>(packetSender).Should().ContainSingle();
    }

    [TestMethod]
    public async Task SomebodyMissingRefusesTheLaunchAndNamesOnlyThoseMissing()
    {
        Player clicking = InWorld("Clicking", inCockpit);
        InWorld("Aboard", cockpitCenter);
        InWorld("Zed", onPlatformDeck);
        InWorld("Diver", new NitroxVector3(-50f, -120f, 400f));

        await RequestLaunch(clicking);

        ConnectedPlayers.SentToAll<RocketLaunch>(packetSender).Should().BeEmpty();
        RocketLaunchRefused refusal = ConnectedPlayers.SentTo<RocketLaunchRefused>(packetSender, clicking).Should().ContainSingle().Subject;
        refusal.RocketId.Should().Be(rocketId);
        refusal.PlayersNotAboard.Should().Equal("Diver", "Zed");
    }

    [TestMethod]
    public async Task OnlyTheClickingPlayerHearsOfTheRefusal()
    {
        Player clicking = InWorld("Clicking", inCockpit);
        Player missing = InWorld("Missing", onPlatformDeck);

        await RequestLaunch(clicking);

        ConnectedPlayers.SentTo<RocketLaunchRefused>(packetSender, missing).Should().BeEmpty();
        packetSender.ReceivedCalls().Should().ContainSingle();
    }

    [TestMethod]
    public async Task APlayerStillLoadingTheWorldDoesNotBlockTheLaunch()
    {
        Player clicking = InWorld("Clicking", inCockpit);
        Player loading = players.Connect("Loading");
        loading.Position = onPlatformDeck;

        await RequestLaunch(clicking);

        ConnectedPlayers.SentToAll<RocketLaunch>(packetSender).Should().ContainSingle();
    }

    [TestMethod]
    public async Task APlayerInABaseOrCyclopsIsNotAboardEvenWhenClose()
    {
        // A player in a sub is never in the cockpit, however close its position; the clients skip it too.
        Player clicking = InWorld("Clicking", inCockpit);
        Player inSub = InWorld("InSub", cockpitCenter);
        inSub.SubRootId = Optional.Of(new NitroxId());

        await RequestLaunch(clicking);

        ConnectedPlayers.SentTo<RocketLaunchRefused>(packetSender, clicking).Should().ContainSingle().Which.PlayersNotAboard.Should().Equal("InSub");
    }

    [TestMethod]
    public async Task ACockpitThatIsNotThisRocketsCountsNobodyAboard()
    {
        // The cockpit's place comes from the client; one far from the rocket, or a rocket the server does not know,
        // cannot be trusted to say who is aboard.
        Player clicking = InWorld("Clicking", new NitroxVector3(900f, 35f, 900f));

        await rocketLaunchManager.RequestLaunchAsync(clicking, new RocketLaunchRequest(rocketId, new NitroxVector3(900f, 35f, 900f)));
        await rocketLaunchManager.RequestLaunchAsync(clicking, new RocketLaunchRequest(new NitroxId(), new NitroxVector3(900f, 35f, 900f)));

        ConnectedPlayers.SentToAll<RocketLaunch>(packetSender).Should().BeEmpty();
        ConnectedPlayers.SentTo<RocketLaunchRefused>(packetSender, clicking).Should().HaveCount(2).And.OnlyContain(refusal => refusal.PlayersNotAboard.Contains("Clicking"));
    }

    [TestMethod]
    public async Task AnEntityThatIsNoRocketIsRefused()
    {
        NitroxId seamothId = AddEntity("Seamoth", rocketPosition);
        Player clicking = InWorld("Clicking", inCockpit);

        await rocketLaunchManager.RequestLaunchAsync(clicking, new RocketLaunchRequest(seamothId, cockpitCenter));

        ConnectedPlayers.SentToAll<RocketLaunch>(packetSender).Should().BeEmpty();
    }

    private Task RequestLaunch(Player player)
    {
        return rocketLaunchManager.RequestLaunchAsync(player, new RocketLaunchRequest(rocketId, cockpitCenter));
    }

    private Player InWorld(string name, NitroxVector3 position)
    {
        Player player = players.Connect(name);
        player.Position = position;
        player.HasFinishedInitialSync = true;
        return player;
    }

    private NitroxId AddEntity(string techType, NitroxVector3 position)
    {
        VehicleEntity entity = new(new NitroxId(), 0f, new NitroxTransform(position, NitroxQuaternion.Identity, NitroxVector3.One), null, false, new NitroxId(), new NitroxTechType(techType), null);
        entityRegistry.AddEntity(entity);
        return entity.Id;
    }
}
