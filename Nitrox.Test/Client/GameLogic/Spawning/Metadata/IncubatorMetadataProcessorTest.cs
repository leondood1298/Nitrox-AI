using NitroxClient.GameLogic.Spawning.Metadata.Processor;

namespace Nitrox.Test.Client.GameLogic.Spawning.Metadata;

[TestClass]
public sealed class IncubatorMetadataProcessorTest
{
    [TestMethod]
    public void HatchingIsPlayedForAPlayerWhoSawTheIncubatorWaiting()
    {
        IncubatorMetadataProcessor.IsHatchingSeenHere(true, false, true).Should().BeTrue();
    }

    [TestMethod]
    public void HatchedStateIsOnlyRestoredWhenTheIncubatorHasJustSpawned()
    {
        // A freshly spawned incubator shows the prefab's state, unpowered, and its metadata comes all at once
        IncubatorMetadataProcessor.IsHatchingSeenHere(false, false, true).Should().BeFalse();
    }

    [TestMethod]
    public void NothingIsPlayedForAnIncubatorThatIsAlreadyHatchedOrNotHatched()
    {
        IncubatorMetadataProcessor.IsHatchingSeenHere(true, true, true).Should().BeFalse();
        IncubatorMetadataProcessor.IsHatchingSeenHere(true, false, false).Should().BeFalse();
    }
}
