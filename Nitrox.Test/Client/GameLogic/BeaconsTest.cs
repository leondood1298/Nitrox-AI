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

    [TestMethod]
    public void ABeaconLyingInTheWorldHumsAlsoWhenItWasThereBeforeTheJoin()
    {
        Beacons.ShouldHum(false, false, false).Should().BeTrue();
    }

    [TestMethod]
    public void AHummingBeaconIsNotStartedAgain()
    {
        Beacons.ShouldHum(true, false, false).Should().BeFalse();
    }

    [TestMethod]
    public void ABeaconInHandOrInAnInventoryOrLockerIsSilent()
    {
        Beacons.ShouldHum(false, true, false).Should().BeFalse();
        Beacons.ShouldHum(false, false, true).Should().BeFalse();
    }
}
