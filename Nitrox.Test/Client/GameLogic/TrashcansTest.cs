using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class TrashcansTest
{
    [TestMethod]
    public void ThePlayerWhoPutTheItemInReportsItsDeletion()
    {
        Trashcans.ShouldReport(true, false).Should().BeTrue();
    }

    [TestMethod]
    public void TheSimulatingPlayerReportsItsDeletion()
    {
        Trashcans.ShouldReport(false, true).Should().BeTrue();
    }

    [TestMethod]
    public void AnyOtherPlayerOnlyDeletesItLocally()
    {
        Trashcans.ShouldReport(false, false).Should().BeFalse();
    }
}
