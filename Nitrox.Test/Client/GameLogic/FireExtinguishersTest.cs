using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class FireExtinguishersTest
{
    [TestMethod]
    public void TheFuelIsReportedOncePerFiveUnitsSpent()
    {
        FireExtinguishers.ShouldReportFuel(84.9f, 81.2f, 100f).Should().BeFalse();
        FireExtinguishers.ShouldReportFuel(80.3f, 79.8f, 100f).Should().BeTrue();
    }

    [TestMethod]
    public void AnEmptyExtinguisherIsReportedEmpty()
    {
        FireExtinguishers.ShouldReportFuel(0.4f, 0f, 100f).Should().BeTrue();
    }

    [TestMethod]
    public void AFullExtinguisherIsReportedFull()
    {
        FireExtinguishers.ShouldReportFuel(99f, 100f, 100f).Should().BeTrue();
    }

    [TestMethod]
    public void UnchangedFuelIsNotReported()
    {
        FireExtinguishers.ShouldReportFuel(100f, 100f, 100f).Should().BeFalse();
        FireExtinguishers.ShouldReportFuel(0f, 0f, 100f).Should().BeFalse();
    }
}
