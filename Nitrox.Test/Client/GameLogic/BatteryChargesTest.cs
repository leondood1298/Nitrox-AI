using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BatteryChargesTest
{
    [TestMethod]
    public void AChargeIsReportedOncePerWholeUnit()
    {
        BatteryCharges.ShouldReport(50.3f, 50.1f, 100f).Should().BeFalse();
        BatteryCharges.ShouldReport(50.3f, 49.9f, 100f).Should().BeTrue();
    }

    [TestMethod]
    public void AFullBatteryIsReportedFull()
    {
        BatteryCharges.ShouldReport(99.6f, 100f, 100f).Should().BeTrue();
        BatteryCharges.ShouldReport(199.7f, 200f, 200f).Should().BeTrue();
    }

    [TestMethod]
    public void AnEmptyBatteryIsReportedEmpty()
    {
        BatteryCharges.ShouldReport(0.4f, 0f, 100f).Should().BeTrue();
    }

    [TestMethod]
    public void AnUnchangedChargeIsNotReported()
    {
        BatteryCharges.ShouldReport(100f, 100f, 100f).Should().BeFalse();
        BatteryCharges.ShouldReport(0f, 0f, 100f).Should().BeFalse();
    }
}
