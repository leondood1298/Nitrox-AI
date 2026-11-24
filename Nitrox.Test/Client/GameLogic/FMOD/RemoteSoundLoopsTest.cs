using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic.FMOD;

namespace Nitrox.Test.Client.GameLogic.FMOD;

[TestClass]
public sealed class RemoteSoundLoopsTest
{
    private const string LOOP = "event:/tools/knife/heat_idle";

    [TestMethod]
    public void ALoopStartedOnAnEntityNotLoadedIsKeptForWhenItLoads()
    {
        RemoteSoundLoops loops = new();
        NitroxId knife = new();

        loops.Started(knife, LOOP, 0.8f);

        loops.TryGetVolume(knife, LOOP, out float volume).Should().BeTrue();
        volume.Should().Be(0.8f);
    }

    [TestMethod]
    public void AStoppedLoopIsNotStartedLater()
    {
        RemoteSoundLoops loops = new();
        NitroxId knife = new();

        loops.Started(knife, LOOP, 1f);
        loops.Stopped(knife, LOOP);

        loops.TryGetVolume(knife, LOOP, out _).Should().BeFalse();
    }

    [TestMethod]
    public void LoopsOfOtherEntitiesStayApart()
    {
        RemoteSoundLoops loops = new();
        NitroxId first = new();
        NitroxId second = new();

        loops.Started(first, LOOP, 1f);
        loops.Stopped(second, LOOP);

        loops.TryGetVolume(first, LOOP, out _).Should().BeTrue();
        loops.TryGetVolume(second, LOOP, out _).Should().BeFalse();
    }
}
