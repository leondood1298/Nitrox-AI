namespace Nitrox.Model.Helper;

[TestClass]
public class HashTreeTest
{
    [TestMethod]
    public void ShouldTestForSameValue()
    {
        HashTree root = new();
        root.AddMoveNext("one").AddMoveNext("two").AddMoveNext("three");

        HashTree test = new();
        test.AddMoveNext("one");
        root.Contains(test).Should().BeTrue();
        test.AddMoveNext("one");
        root.Contains(test).Should().BeTrue();
        test.AddMoveNext("one").AddMoveNext("two");
        root.Contains(test).Should().BeTrue();
        test.AddMoveNext("one").AddMoveNext("two").AddMoveNext("3");
        root.Contains(test).Should().BeFalse();
    }

    [TestMethod]
    public void AClearedTreeTakesWhatItHeldAgain()
    {
        HashTree root = new();
        HashTree branch = new();
        branch.AddMoveNext("can't update entity position of ").AddMoveNext("some id");
        root.TryAdd(branch).Should().BeTrue();
        root.TryAdd(branch).Should().BeFalse();

        root.Clear();

        root.TryAdd(branch).Should().BeTrue();
    }
}
