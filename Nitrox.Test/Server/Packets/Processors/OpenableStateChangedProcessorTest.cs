using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class OpenableStateChangedProcessorTest
{
    [TestMethod]
    public void ADoorsStateIsKeptForPlayersWhoJoinLater()
    {
        PathBasedChildEntity door = new("Doors/Door1", new NitroxId(), null, null, new NitroxId(), []);

        Assert.IsTrue(OpenableStateChangedProcessor.TryStore(door, true));
        Assert.IsTrue(((OpenableMetadata)door.Metadata).IsOpen);

        Assert.IsTrue(OpenableStateChangedProcessor.TryStore(door, false));
        Assert.IsFalse(((OpenableMetadata)door.Metadata).IsOpen);
    }

    [TestMethod]
    public void ADoorsStateNeverReplacesOtherMetadata()
    {
        CyclopsLightingMetadata lighting = new(true, true);
        PathBasedChildEntity panel = new("LightingPanel", new NitroxId(), null, lighting, new NitroxId(), []);

        Assert.IsFalse(OpenableStateChangedProcessor.TryStore(panel, true));
        Assert.AreSame(lighting, panel.Metadata);
    }
}
