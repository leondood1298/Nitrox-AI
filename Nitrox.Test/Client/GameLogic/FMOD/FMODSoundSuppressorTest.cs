using NitroxClient.GameLogic.FMOD;

namespace Nitrox.Test.Client.GameLogic.FMOD;

[TestClass]
public sealed class FMODSoundSuppressorTest
{
    [TestMethod]
    public void AnInnerScopeEndingDoesNotEndTheOuterOne()
    {
        FMODSoundSuppressor.SuppressFMODEvents.Should().BeFalse();
        using (FMODSystem.SuppressSubnauticaSounds())
        {
            using (FMODSystem.SuppressSubnauticaSounds())
            {
                FMODSoundSuppressor.SuppressFMODEvents.Should().BeTrue();
            }
            FMODSoundSuppressor.SuppressFMODEvents.Should().BeTrue("the outer scope still holds");
        }
        FMODSoundSuppressor.SuppressFMODEvents.Should().BeFalse();
    }
}
