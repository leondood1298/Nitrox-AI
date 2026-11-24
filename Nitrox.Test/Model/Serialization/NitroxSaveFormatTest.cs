using Nitrox.Model.Serialization;

namespace Nitrox.Test.Model.Serialization;

[TestClass]
public sealed class NitroxSaveFormatTest
{
    [TestMethod]
    public void CurrentFormatIsAccepted()
    {
        NitroxSaveFormat.DescribeMismatch("My World", NitroxSaveFormat.CURRENT).Should().BeNull();
    }

    [TestMethod]
    public void FormatOfThePreviousBuildIsStillRead()
    {
        NitroxSaveFormat.DescribeMismatch("My World", NitroxSaveFormat.OLDEST_READABLE).Should().BeNull();
        NitroxSaveFormat.OLDEST_READABLE.Should().BeLessThan(NitroxSaveFormat.CURRENT);
    }

    [TestMethod]
    public void OlderFormatIsRefusedAndTheBackupsAreNamed()
    {
        string? message = NitroxSaveFormat.DescribeMismatch("My World", NitroxSaveFormat.UNKNOWN);

        message.Should().Contain("\"My World\"").And.Contain("an older build").And.Contain("backups");
    }

    [TestMethod]
    public void NewerFormatIsRefused()
    {
        NitroxSaveFormat.DescribeMismatch("My World", NitroxSaveFormat.CURRENT + 1).Should().Contain("a newer build");
    }
}
