using Nitrox.Model.Serialization;

namespace Nitrox.Test.Model.Serialization;

[TestClass]
public sealed class ServerListTest
{
    [TestMethod]
    public void ALineWithoutANameIsLeftOutInsteadOfBreakingTheList()
    {
        ServerList.Entry.FromLine("|127.0.0.1|11000").Should().BeNull();
        ServerList.Entry.FromLine("   |127.0.0.1").Should().BeNull();
    }

    [TestMethod]
    public void ALineWithoutAnAddressIsLeftOut()
    {
        ServerList.Entry.FromLine("My server| |11000").Should().BeNull();
    }

    [TestMethod]
    public void ALineWithAPortOutsideTheRangeIsLeftOut()
    {
        ServerList.Entry.FromLine("My server|127.0.0.1|0").Should().BeNull();
        ServerList.Entry.FromLine("My server|127.0.0.1|70000").Should().BeNull();
        ServerList.Entry.FromLine("My server|127.0.0.1|-5").Should().BeNull();
    }

    [TestMethod]
    public void AGoodLineIsReadBack()
    {
        ServerList.Entry entry = ServerList.Entry.FromLine("My server|example.org|11001");
        entry.Should().NotBeNull();
        entry!.Name.Should().Be("My server");
        entry.Address.Should().Be("example.org");
        entry.Port.Should().Be(11001);
        ServerList.Entry.FromLine(entry.ToString())!.ToString().Should().Be(entry.ToString());
    }

    [TestMethod]
    public void DeletingAServerRemovesThatEntryWhereverFoundServersSitInTheList()
    {
        ServerList list = new();
        ServerList.Entry saved = new("Saved", "127.0.0.1", 11000);
        ServerList.Entry found = new("Found", "192.168.0.5", 11000, false);
        ServerList.Entry other = new("Other", "example.org", 11000);
        list.Add(saved);
        list.Add(found);
        list.Add(other);

        list.Remove(found).Should().BeTrue();

        list.Entries.Should().Equal(saved, other);
    }

    [TestMethod]
    public void ANameWithTheFieldSeparatorCannotBeSaved()
    {
        ServerList.IsValidName("Ana|s server").Should().BeFalse();
        ServerList.IsValidName("  ").Should().BeFalse();
        ServerList.IsValidName("Ana's server").Should().BeTrue();
    }

    [TestMethod]
    public void OnlyPortsAConnectionCanUseAreValid()
    {
        ServerList.IsValidPort(0).Should().BeFalse();
        ServerList.IsValidPort(65536).Should().BeFalse();
        ServerList.IsValidPort(1).Should().BeTrue();
        ServerList.IsValidPort(65535).Should().BeTrue();
    }
}
