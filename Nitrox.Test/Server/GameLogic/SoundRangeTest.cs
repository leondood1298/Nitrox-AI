using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.GameLogic.FMOD;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

[TestClass]
public sealed class SoundRangeTest
{
    private static readonly SoundData doorSound = new(true, false, 20f);

    [TestMethod]
    public void AListenerWithinTheRadiusHearsIt()
    {
        SoundRange.Reaches(doorSound, NitroxVector3.Zero, Optional.Empty, new NitroxVector3(15, 0, 0), Optional.Empty).Should().BeTrue();
    }

    [TestMethod]
    public void AListenerBeyondTheRadiusDoesNot()
    {
        SoundRange.Reaches(doorSound, NitroxVector3.Zero, Optional.Empty, new NitroxVector3(25, 0, 0), Optional.Empty).Should().BeFalse();
    }

    [TestMethod]
    public void ASoundThatStaysInItsSubIsNotHeardFromAnother()
    {
        NitroxId cyclops = new();
        SoundRange.Reaches(doorSound, NitroxVector3.Zero, Optional.Of(cyclops), new NitroxVector3(5, 0, 0), Optional.Empty).Should().BeFalse();
        SoundRange.Reaches(new SoundData(true, true, 20f), NitroxVector3.Zero, Optional.Of(cyclops), new NitroxVector3(5, 0, 0), Optional.Empty).Should().BeTrue();
    }
}
