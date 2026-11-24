using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.Helper;

namespace Nitrox.Test.Server.Helper;

[TestClass]
public sealed class RandomFactoryTest
{
    [TestMethod]
    public void ABatchIsFilledWithTheSameRandomWhicheverBatchesCameBefore()
    {
        RandomFactory first = NewFactory("GNKKHHRRTZ");
        RandomFactory second = NewFactory("GNKKHHRRTZ");
        NitroxInt3 batch = new(10, 18, 10);

        // The second server fills another batch first
        second.GetBatchRandom(new NitroxInt3(1, 2, 3)).NextFloat();

        Sequence(first.GetBatchRandom(batch)).Should().Equal(Sequence(second.GetBatchRandom(batch)));
    }

    [TestMethod]
    public void TwoBatchesOrTwoSeedsAreFilledDifferently()
    {
        RandomFactory factory = NewFactory("GNKKHHRRTZ");

        Sequence(factory.GetBatchRandom(new NitroxInt3(10, 18, 10))).Should().NotEqual(Sequence(factory.GetBatchRandom(new NitroxInt3(10, 18, 11))));
        Sequence(factory.GetBatchRandom(new NitroxInt3(10, 18, 10))).Should().NotEqual(Sequence(NewFactory("OTHERSEED").GetBatchRandom(new NitroxInt3(10, 18, 10))));
    }

    private static RandomFactory NewFactory(string seed) => new(Options.Create(new SubnauticaServerOptions { Seed = seed }));

    private static List<uint> Sequence(XorRandom random) => [.. Enumerable.Range(0, 8).Select(_ => random.NextUInt())];
}
