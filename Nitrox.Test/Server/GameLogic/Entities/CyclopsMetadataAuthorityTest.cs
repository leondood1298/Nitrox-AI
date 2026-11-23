using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class CyclopsMetadataAuthorityTest
{
    [TestMethod]
    public void ButtonPressByAnotherPlayerCannotOverwriteHullHealthOrDestruction()
    {
        // A passenger toggles silent running. Their copy of the hull still shows full health.
        CyclopsMetadata stored = new(false, false, false, true, 1, 310f, false);
        CyclopsMetadata fromPassenger = new(true, false, false, true, 1, 1500f, false);

        CyclopsMetadata accepted = CyclopsMetadataAuthority.Merge(stored, fromPassenger, false);

        Assert.IsTrue(accepted.SilentRunningOn, "The button press itself is accepted.");
        Assert.AreEqual(310f, accepted.Health);
        Assert.IsFalse(accepted.IsDestroyed);
    }

    [TestMethod]
    public void StalePassengerCopyCannotBringADestroyedCyclopsBack()
    {
        CyclopsMetadata stored = new(false, false, false, false, 0, 0f, true);
        CyclopsMetadata fromPassenger = new(false, true, false, false, 0, 900f, false);

        CyclopsMetadata accepted = CyclopsMetadataAuthority.Merge(stored, fromPassenger, false);

        Assert.IsTrue(accepted.IsDestroyed);
        Assert.AreEqual(0f, accepted.Health);
        Assert.IsTrue(accepted.ShieldOn);
    }

    [TestMethod]
    public void SimulatingPlayerSetsHullHealth()
    {
        CyclopsMetadata stored = new(false, false, false, true, 1, 1500f, false);
        CyclopsMetadata fromSimulator = new(false, false, false, true, 1, 740f, false);

        Assert.AreSame(fromSimulator, CyclopsMetadataAuthority.Merge(stored, fromSimulator, true));
    }

    [TestMethod]
    public void FirstMetadataIsAcceptedAsSent()
    {
        CyclopsMetadata first = new(false, false, false, true, 1, 1500f, false);

        Assert.AreSame(first, CyclopsMetadataAuthority.Merge(null, first, false));
    }
}
