using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class ActiveTeleportersTest
{
    [TestMethod]
    public void OnlyTheFirstActivationOfAnIdentifierIsNew()
    {
        ActiveTeleporters teleporters = new();

        teleporters.Add("crashedship").Should().BeTrue();
        teleporters.Add("crashedship").Should().BeFalse("every client that loads an open teleporter reports it again");
        teleporters.Add("precursorgun").Should().BeTrue();

        teleporters.GetAll().Should().Equal("crashedship", "precursorgun");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void EmptyIdentifierIsNotStored(string identifier)
    {
        ActiveTeleporters teleporters = new();

        teleporters.Add(identifier).Should().BeFalse();
        teleporters.GetAll().Should().BeEmpty();
    }

    [TestMethod]
    public void LoadReplacesWhatWasActiveAndAcceptsASaveWithoutTeleporters()
    {
        ActiveTeleporters teleporters = new();
        teleporters.Add("lostriverbase");

        teleporters.Load(["prison", "", "crashedship"]);
        teleporters.GetAll().Should().Equal("crashedship", "prison");

        teleporters.Load(null);
        teleporters.GetAll().Should().BeEmpty();
    }
}
