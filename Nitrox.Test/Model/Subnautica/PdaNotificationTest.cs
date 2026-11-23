using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class PdaNotificationTest
{
    [TestMethod]
    public void MarkersOfTheSameGroupAndKeyAreTheSame()
    {
        new PdaNotification(PdaNotificationGroup.LOG, "Story_AuroraWarning1").Should().Be(new PdaNotification(PdaNotificationGroup.LOG, "Story_AuroraWarning1"));
        new PdaNotification(PdaNotificationGroup.LOG, "757").Should().NotBe(new PdaNotification(PdaNotificationGroup.BLUEPRINTS, "757"));
    }

    [TestMethod]
    public void OnlyTheGroupsOfThePdaAreKept()
    {
        // NotificationManager.Group: Inventory 1, Gallery 6 and Pings 8 are not about the PDA's content
        new[] { 2, 3, 4, 5, 7 }.Should().OnlyContain(group => PdaNotification.IsKept(group));
        new[] { 0, 1, 6, 8 }.Should().NotContain(group => PdaNotification.IsKept(group));
    }

    [TestMethod]
    public void ABlueprintIsMarkedInTheBlueprintsTheCraftTreeAndTheBuilder()
    {
        PdaNotification.ForBlueprint("757").Should().BeEquivalentTo([
            new PdaNotification(PdaNotificationGroup.BLUEPRINTS, "757"),
            new PdaNotification(PdaNotificationGroup.CRAFT_TREE, "757"),
            new PdaNotification(PdaNotificationGroup.BUILDER, "757")
        ]);
    }
}
