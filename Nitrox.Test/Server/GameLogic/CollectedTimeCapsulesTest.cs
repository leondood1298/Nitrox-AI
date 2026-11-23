using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class CollectedTimeCapsulesTest
{
    [TestMethod]
    public void OnlyTheFirstCollectionOfAContentIsNew()
    {
        CollectedTimeCapsules capsules = new();
        CollectedTimeCapsule first = new("capsule-b", "Greetings", "We were here", "b.jpg");

        capsules.Add(first).Should().BeTrue();
        capsules.Add(new CollectedTimeCapsule("capsule-b", "Other", "Other", "other.jpg")).Should().BeFalse("two capsules in the world can hold the same content");
        capsules.Add(new CollectedTimeCapsule("capsule-a", "Hi", "", null)).Should().BeTrue();

        capsules.GetAll().Select(capsule => capsule.Id).Should().Equal("capsule-a", "capsule-b");
        capsules.GetAll().Single(capsule => capsule.Id == "capsule-b").Should().BeSameAs(first, "the content of the first collection is kept");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void CapsuleWithoutIdIsNotStored(string id)
    {
        CollectedTimeCapsules capsules = new();

        capsules.Add(new CollectedTimeCapsule(id, "Title", "Text", "image.jpg")).Should().BeFalse();
        capsules.Add(null).Should().BeFalse();
        capsules.GetAll().Should().BeEmpty();
    }

    [TestMethod]
    public void LoadReplacesWhatWasCollectedAndAcceptsASaveWithoutCapsules()
    {
        CollectedTimeCapsules capsules = new();
        capsules.Add(new CollectedTimeCapsule("capsule-c", "", "", ""));

        capsules.Load([new CollectedTimeCapsule("capsule-a", "", "", ""), new CollectedTimeCapsule("", "", "", ""), null]);
        capsules.GetAll().Select(capsule => capsule.Id).Should().Equal("capsule-a");

        capsules.Load(null);
        capsules.GetAll().Should().BeEmpty();
    }
}
