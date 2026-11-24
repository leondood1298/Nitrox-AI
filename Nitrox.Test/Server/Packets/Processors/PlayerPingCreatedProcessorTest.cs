using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class PlayerPingCreatedProcessorTest
{
    [TestMethod]
    public void APingIsRelayedAsComingFromItsSenderWhateverSessionItNamed()
    {
        PlayerPingCreated sent = new((SessionId)7, "Peeper", "Peeper", new NitroxVector3(1, 2, 3), new NitroxId());

        PlayerPingCreated relayed = PlayerPingCreatedProcessor.Sanitize(sent, (SessionId)3);

        Assert.AreEqual((SessionId)3, relayed.SessionId);
        Assert.AreEqual("Peeper", relayed.TargetTechType);
        Assert.AreEqual(sent.Position, relayed.Position);
        Assert.AreEqual(sent.PingId, relayed.PingId);
    }

    [TestMethod]
    public void OverlongOrMissingTextsAreBounded()
    {
        PlayerPingCreated sent = new((SessionId)3, null!, new string('x', 10_000), NitroxVector3.Zero, new NitroxId());

        PlayerPingCreated relayed = PlayerPingCreatedProcessor.Sanitize(sent, (SessionId)3);

        Assert.AreEqual(string.Empty, relayed.TargetTechType);
        Assert.AreEqual(PlayerPingCreatedProcessor.MAX_TEXT_LENGTH, relayed.TargetName.Length);
    }
}
