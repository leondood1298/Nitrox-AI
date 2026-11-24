using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class ItemSourcesTest
{
    private float now;
    private ItemSources itemSources;

    [TestInitialize]
    public void Init()
    {
        now = 0;
        itemSources = new ItemSources(() => now);
    }

    [TestMethod]
    public void AnAnnouncedItemCarriesItsSource()
    {
        NitroxId cabinet = new();
        itemSources.Expect(TechType.FirstAidKit, cabinet, "medkit");

        ItemSource? source = itemSources.Take(TechType.FirstAidKit, ItemSourceUse.PICKUP);

        source.Should().NotBeNull();
        source!.SourceId.Should().Be(cabinet);
        source.Token.Should().Be("medkit");
        itemSources.Take(TechType.FirstAidKit, ItemSourceUse.PICKUP).Should().BeNull();
    }

    [TestMethod]
    public void AnItemOfAnotherKindHasNoSource()
    {
        itemSources.Expect(TechType.FirstAidKit, new NitroxId(), "medkit");

        itemSources.Take(TechType.Titanium, ItemSourceUse.PICKUP).Should().BeNull();
        itemSources.Take(TechType.FirstAidKit, ItemSourceUse.DROP).Should().BeNull();
    }

    [TestMethod]
    public void AnAnnouncementIsForgottenAfterAWhile()
    {
        itemSources.Expect(TechType.FirstAidKit, new NitroxId(), "medkit");

        now = ItemSources.EXPECTATION_LIFETIME + 1;

        itemSources.Take(TechType.FirstAidKit, ItemSourceUse.PICKUP).Should().BeNull();
    }

    [TestMethod]
    public void SeveralItemsOfOneKindAreNumbered()
    {
        NitroxId fabricator = new();
        itemSources.Expect(TechType.Titanium, fabricator, "craft", 2);

        itemSources.Take(TechType.Titanium, ItemSourceUse.PICKUP)!.Token.Should().Be("craft:0");
        itemSources.Take(TechType.Titanium, ItemSourceUse.PICKUP)!.Token.Should().Be("craft:1");
        itemSources.Take(TechType.Titanium, ItemSourceUse.PICKUP).Should().BeNull();
    }

    [TestMethod]
    public void ItemsOfAnyKindShareTheirToken()
    {
        NitroxId outcrop = new();
        itemSources.ExpectAny(ItemSourceUse.DROP, outcrop, "break", 3);

        itemSources.Take(TechType.Titanium, ItemSourceUse.DROP)!.Token.Should().Be("break");
        itemSources.Take(TechType.Copper, ItemSourceUse.DROP)!.Token.Should().Be("break");
    }

    [TestMethod]
    public void AnItemsOwnKindComesBeforeAnyKind()
    {
        NitroxId capsule = new();
        NitroxId cabinet = new();
        itemSources.ExpectAny(ItemSourceUse.PICKUP, capsule, "capsule", 1);
        itemSources.Expect(TechType.FirstAidKit, cabinet, "medkit");

        itemSources.Take(TechType.FirstAidKit, ItemSourceUse.PICKUP)!.SourceId.Should().Be(cabinet);
        itemSources.Take(TechType.Titanium, ItemSourceUse.PICKUP)!.SourceId.Should().Be(capsule);
    }

    [TestMethod]
    [DataRow(200f, false, DisplayName = "Whole chunk")]
    [DataRow(5.5f, false, DisplayName = "More than one hit left")]
    [DataRow(5f, true, DisplayName = "Exactly one hit left")]
    [DataRow(0.5f, true, DisplayName = "Less than one hit left")]
    [DataRow(0f, false, DisplayName = "Chunk already broken")]
    public void OnlyTheHitThatBreaksAChunkAnnouncesItsResources(float health, bool breaks)
    {
        ItemSources.HitBreaksChunk(health).Should().Be(breaks);
    }

    [TestMethod]
    public void ASecondClickAnnouncesTheSameItemOnce()
    {
        NitroxId cabinet = new();
        itemSources.Expect(TechType.FirstAidKit, cabinet, "medkit");
        itemSources.Expect(TechType.FirstAidKit, cabinet, "medkit");

        itemSources.Take(TechType.FirstAidKit, ItemSourceUse.PICKUP).Should().NotBeNull();
        itemSources.Take(TechType.FirstAidKit, ItemSourceUse.PICKUP).Should().BeNull();
    }
}
