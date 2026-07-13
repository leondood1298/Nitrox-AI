namespace NitroxClient.GameLogic;

[TestClass]
public sealed class ReactorFuelDisplayTest
{
	[TestMethod]
	public void RemainingEnergySubtractsOnlyPartialCurrentFuelProgress()
	{
		Assert.AreEqual(375f, ReactorFuelDisplay.CalculateRemainingEnergy(420f, 45f));
		Assert.AreEqual(0f, ReactorFuelDisplay.CalculateRemainingEnergy(20f, 25f));
	}

	[DataTestMethod]
	[DataRow(30f, "1m")]
	[DataRow(4800f, "1h 20m")]
	[DataRow(7200f, "2h")]
	public void RuntimeIsCompactForHoverText(float seconds, string expected)
	{
		Assert.AreEqual(expected, ReactorFuelDisplay.FormatRuntime(seconds));
	}
}
