using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroxClient.MonoBehaviours;

namespace Nitrox.Test.Client.MonoBehaviours;

[TestClass]
public sealed class NitroxEntityIdentityGuardTest
{
    [TestMethod]
    public void StaleDuplicateCannotClaimCanonicalObjectsRegistration()
    {
        object canonicalRegistration = new();
        object staleDuplicateRegistration = new();

        Assert.IsTrue(NitroxEntity.IsRegistrationOwner(canonicalRegistration, canonicalRegistration));
        Assert.IsFalse(NitroxEntity.IsRegistrationOwner(canonicalRegistration, staleDuplicateRegistration));
        Assert.IsTrue(NitroxEntity.CanClaimRegistration(false, false));
        Assert.IsTrue(NitroxEntity.CanClaimRegistration(true, true));
        Assert.IsFalse(NitroxEntity.CanClaimRegistration(true, false));
    }
}
