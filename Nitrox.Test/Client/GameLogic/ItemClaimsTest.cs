using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class ItemClaimsTest
{
    [TestMethod]
    public void TheLineNamesThePlayerAndTheItem()
    {
        ItemClaims.FormatMessage("{PLAYER} got the {ITEM} first.", "Alice", "First Aid Kit").Should().Be("Alice got the First Aid Kit first.");
    }
}
