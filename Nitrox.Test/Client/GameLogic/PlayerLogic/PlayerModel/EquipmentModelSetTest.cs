using NitroxClient.GameLogic.PlayerLogic.PlayerModel;

namespace Nitrox.Test.Client.GameLogic.PlayerLogic.PlayerModel;

[TestClass]
public sealed class EquipmentModelSetTest
{
    private static EquipmentModelSet<string> CreateSet()
    {
        EquipmentModelSet<string> set = new();
        set.AddSlot("Head", [new(TechType.Rebreather, "rebreather"), new(TechType.RadiationHelmet, "radiation helmet")], "bare head");
        set.AddSlot("Foot", [new(TechType.Fins, "fins"), new(TechType.UltraGlideFins, "glide fins")], null);
        return set;
    }

    private static Dictionary<string, bool> Shown(EquipmentModelSet<string> set, Dictionary<string, TechType> equipped, params string[] hiddenSlots)
    {
        return set.GetVisibility(equipped, hiddenSlots).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    [TestMethod]
    public void ThePlayerWithNothingOnShowsOnlyTheDefaultModels()
    {
        Dictionary<string, bool> shown = Shown(CreateSet(), new Dictionary<string, TechType>());

        shown.Where(pair => pair.Value).Select(pair => pair.Key).Should().BeEquivalentTo("bare head");
    }

    [TestMethod]
    public void AnItemInASlotShowsItsModelInsteadOfTheDefault()
    {
        Dictionary<string, bool> shown = Shown(CreateSet(), new Dictionary<string, TechType> { ["Head"] = TechType.Rebreather, ["Foot"] = TechType.UltraGlideFins });

        shown.Where(pair => pair.Value).Select(pair => pair.Key).Should().BeEquivalentTo("rebreather", "glide fins");
    }

    [TestMethod]
    public void AnItemWithoutAModelLeavesTheDefaultShown()
    {
        Dictionary<string, bool> shown = Shown(CreateSet(), new Dictionary<string, TechType> { ["Head"] = TechType.Titanium });

        shown["bare head"].Should().BeTrue();
        shown["rebreather"].Should().BeFalse();
    }

    [TestMethod]
    public void AHiddenSlotShowsItsDefaultUntilItIsShownAgain()
    {
        EquipmentModelSet<string> set = CreateSet();
        Dictionary<string, TechType> equipped = new() { ["Head"] = TechType.RadiationHelmet };

        Dictionary<string, bool> hidden = Shown(set, equipped, "Head");
        Dictionary<string, bool> shownAgain = Shown(set, equipped);

        hidden["radiation helmet"].Should().BeFalse();
        hidden["bare head"].Should().BeTrue();
        shownAgain["radiation helmet"].Should().BeTrue();
        shownAgain["bare head"].Should().BeFalse();
    }
}
