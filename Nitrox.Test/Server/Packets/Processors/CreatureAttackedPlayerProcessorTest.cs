using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public class CreatureAttackedPlayerProcessorTest
{
    private const ushort SIMULATING_SESSION = 1;
    private const ushort BITTEN_SESSION = 2;

    [TestMethod]
    public void IsReportFromSimulatingPlayer_SenderHoldsLock_ReturnsTrue()
    {
        Player sender = CreatePlayer(1, SIMULATING_SESSION);

        CreatureAttackedPlayerProcessor.IsReportFromSimulatingPlayer(CreateAttack(30f), sender, sender).Should().BeTrue();
    }

    [TestMethod]
    public void IsReportFromSimulatingPlayer_OtherPlayerHoldsLock_ReturnsFalse()
    {
        // Otherwise any player could hurt any other by naming a creature
        CreatureAttackedPlayerProcessor.IsReportFromSimulatingPlayer(CreateAttack(30f), CreatePlayer(3, 3), CreatePlayer(1, SIMULATING_SESSION)).Should().BeFalse();
    }

    [TestMethod]
    public void IsReportFromSimulatingPlayer_NobodyHoldsLock_ReturnsFalse()
    {
        CreatureAttackedPlayerProcessor.IsReportFromSimulatingPlayer(CreateAttack(30f), null, CreatePlayer(1, SIMULATING_SESSION)).Should().BeFalse();
    }

    [TestMethod]
    public void IsReportFromSimulatingPlayer_SamePeerInAnotherSession_ReturnsFalse()
    {
        // Player == compares PeerId: a reconnected player must not pass for its previous session
        CreatureAttackedPlayerProcessor.IsReportFromSimulatingPlayer(CreateAttack(30f), CreatePlayer(1, SIMULATING_SESSION), CreatePlayer(1, 5)).Should().BeFalse();
    }

    [TestMethod]
    public void IsReportFromSimulatingPlayer_TargetIsTheSender_ReturnsFalse()
    {
        // A hit on the simulating player is applied by their own game; passing it back would count it twice
        Player sender = CreatePlayer(1, BITTEN_SESSION);

        CreatureAttackedPlayerProcessor.IsReportFromSimulatingPlayer(CreateAttack(30f), sender, sender).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(0f)]
    [DataRow(-30f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void IsReportFromSimulatingPlayer_DamageIsNotAPositiveNumber_ReturnsFalse(float damage)
    {
        Player sender = CreatePlayer(1, SIMULATING_SESSION);

        CreatureAttackedPlayerProcessor.IsReportFromSimulatingPlayer(CreateAttack(damage), sender, sender).Should().BeFalse();
    }

    private static CreatureAttackedPlayer CreateAttack(float damage)
    {
        return new CreatureAttackedPlayer(new NitroxId(), BITTEN_SESSION, damage, DamageType.Normal);
    }

    private static Player CreatePlayer(ushort peerId, ushort sessionId)
    {
        return new Player(peerId, sessionId, $"Player{sessionId}", false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
                          new PlayerStatsData(100, 100, 100, 100, 100, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
                          new Dictionary<string, PingInstancePreference>(), [], false, true);
    }
}
