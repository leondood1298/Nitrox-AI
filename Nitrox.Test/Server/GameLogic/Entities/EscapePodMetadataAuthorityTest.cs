using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class EscapePodMetadataAuthorityTest
{
    [TestMethod]
    public void AcceptsIndependentPodAndRadioRepairs()
    {
        EscapePodMetadata podRepaired = EscapePodMetadataAuthority.Merge(new(false, false), new(true, false));
        EscapePodMetadata bothRepaired = EscapePodMetadataAuthority.Merge(podRepaired, new(false, true));

        Assert.IsTrue(bothRepaired.PodRepaired);
        Assert.IsTrue(bothRepaired.RadioRepaired);
    }

    [TestMethod]
    public void NeverRegressesPersistedRepairs()
    {
        EscapePodMetadata accepted = EscapePodMetadataAuthority.Merge(new(true, true), new(false, false));

        Assert.IsTrue(accepted.PodRepaired);
        Assert.IsTrue(accepted.RadioRepaired);
    }

    [TestMethod]
    public void SupportsLegacyMissingMetadata()
    {
        EscapePodMetadata accepted = EscapePodMetadataAuthority.Merge(null, new(true, false));

        Assert.IsTrue(accepted.PodRepaired);
        Assert.IsFalse(accepted.RadioRepaired);
    }
}
