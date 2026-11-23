using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Test.Model.Subnautica;

/// <summary>
///     The server and every client decide with this rule who is aboard, so a player the server counted for a launch also
///     takes part in it.
/// </summary>
[TestClass]
public sealed class RocketCockpitTest
{
    private static readonly NitroxVector3 cockpitCenter = new(100f, 40f, -200f);

    [TestMethod]
    [DataRow(0f, 0f, 0f)]
    [DataRow(3f, -1.5f, 2f)]
    [DataRow(0f, -5.5f, 0f)]
    public void APlayerInTheCockpitIsAboard(float x, float y, float z)
    {
        RocketCockpit.IsAboard(cockpitCenter, cockpitCenter + new NitroxVector3(x, y, z)).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(0f, -5.6f, 0f)]
    [DataRow(4f, 0f, 4f)]
    [DataRow(0f, -40f, 0f)]
    public void APlayerOutsideTheCockpitIsNotAboard(float x, float y, float z)
    {
        // The last row stands for the platform's deck, two stages below the cockpit.
        RocketCockpit.IsAboard(cockpitCenter, cockpitCenter + new NitroxVector3(x, y, z)).Should().BeFalse();
    }

    [TestMethod]
    public void ACockpitFarFromTheRocketCannotBeItsCockpit()
    {
        NitroxVector3 rocket = new(100f, 0f, -200f);

        RocketCockpit.CanBeCockpitOf(rocket, cockpitCenter).Should().BeTrue();
        RocketCockpit.CanBeCockpitOf(rocket, new NitroxVector3(500f, 40f, -200f)).Should().BeFalse();
    }
}
