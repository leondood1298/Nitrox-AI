using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BeaconsTest
{
    [TestMethod]
    public void ARenameByThePlayerIsSent()
    {
        Beacons.ShouldBroadcastLabel("Beacon", "Base entrance", false, true).Should().BeTrue();
    }

    [TestMethod]
    public void SettingTheSameLabelAgainIsNotSent()
    {
        Beacons.ShouldBroadcastLabel("Base entrance", "Base entrance", false, true).Should().BeFalse();
    }

    [TestMethod]
    public void TheLabelSetWhenTheBeaconStartsIsNotSent()
    {
        Beacons.ShouldBroadcastLabel("", "Beacon", true, true).Should().BeFalse();
    }

    [TestMethod]
    public void NothingIsSentWhileJoining()
    {
        Beacons.ShouldBroadcastLabel("Beacon", "Base entrance", false, false).Should().BeFalse();
    }
}
