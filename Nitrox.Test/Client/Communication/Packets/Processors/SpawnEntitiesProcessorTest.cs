using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors;

namespace Nitrox.Test.Client.Communication.Packets.Processors;

[TestClass]
public sealed class SpawnEntitiesProcessorTest
{
    private static readonly WorldEntity simulatedFish = NewFish();
    private static readonly WorldEntity otherFish = NewFish();

    [TestMethod]
    public void TheAnswerToLoadedCellsDoesNotReplaceWhatThisGameSimulates()
    {
        SpawnEntities answer = new([simulatedFish, otherFish], [], [simulatedFish.AbsoluteEntityCell], true);

        SpawnEntitiesProcessor.SelectForCleanup(answer, id => id == simulatedFish.Id).Should().ContainSingle().Which.Should().BeSameAs(otherFish);
    }

    [TestMethod]
    public void ACorrectionFromTheServerReplacesEverything()
    {
        SpawnEntities correction = new(simulatedFish, null, true);

        SpawnEntitiesProcessor.SelectForCleanup(correction, id => id == simulatedFish.Id).Should().ContainSingle().Which.Should().BeSameAs(simulatedFish);
    }

    private static WorldEntity NewFish()
    {
        return new WorldEntity(new NitroxVector3(10, -20, 30), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Peeper"), 0, "peeper", true, new NitroxId(), null);
    }
}
