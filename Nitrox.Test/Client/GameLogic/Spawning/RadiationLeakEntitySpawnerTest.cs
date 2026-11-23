using NitroxClient.GameLogic.Spawning.WorldEntities;

namespace Nitrox.Test.Client.GameLogic.Spawning;

[TestClass]
public sealed class RadiationLeakEntitySpawnerTest
{
    private const int TOTAL_LEAKS = 11;

    [TestMethod]
    public void NoLeakFixedIsNotAllFixed()
    {
        RadiationLeakEntitySpawner.AreAllLeaksFixed(Enumerable.Repeat(-1f, TOTAL_LEAKS)).Should().BeFalse();
    }

    [TestMethod]
    public void OneOfElevenLeaksFixedIsNotAllFixed()
    {
        // The highest fix time is then positive, which made a joiner see the radiation as gone
        List<float> fixTimes = Enumerable.Repeat(-1f, TOTAL_LEAKS).ToList();
        fixTimes[4] = 1234.5f;

        RadiationLeakEntitySpawner.AreAllLeaksFixed(fixTimes).Should().BeFalse();
    }

    [TestMethod]
    public void EveryLeakFixedIsAllFixed()
    {
        List<float> fixTimes = Enumerable.Range(0, TOTAL_LEAKS).Select(i => 100f + i).ToList();
        fixTimes[0] = 0f;

        RadiationLeakEntitySpawner.AreAllLeaksFixed(fixTimes).Should().BeTrue();
    }
}
