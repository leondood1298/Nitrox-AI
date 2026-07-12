using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class MapRoomMetadataAuthorityTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    [TestMethod]
    public void TargetChangeAdvancesGenerationAndResetsProgress()
    {
        MapRoomMetadata current = new(quartz, 8, 3, 12);
        MapRoomMetadata requested = new(titanium, 8, 3, 12);

        bool result = MapRoomMetadataAuthority.TryAccept(current, requested, out MapRoomMetadata accepted);

        Assert.IsTrue(result);
        Assert.AreEqual(titanium, accepted.TypeToScan);
        Assert.AreEqual(0, accepted.NumNodesScanned);
        Assert.AreEqual(4, accepted.Generation);
        Assert.AreEqual(13, accepted.Revision);
    }

    [TestMethod]
    public void ProgressUpdateAdvancesRevisionOnly()
    {
        MapRoomMetadata current = new(quartz, 2, 4, 9);
        MapRoomMetadata requested = new(quartz, 3, 4, 9);

        bool result = MapRoomMetadataAuthority.TryAccept(current, requested, out MapRoomMetadata accepted);

        Assert.IsTrue(result);
        Assert.AreEqual(3, accepted.NumNodesScanned);
        Assert.AreEqual(4, accepted.Generation);
        Assert.AreEqual(10, accepted.Revision);
    }

    [TestMethod]
    public void StaleGenerationIsRejected()
    {
        MapRoomMetadata current = new(quartz, 2, 4, 9);
        MapRoomMetadata requested = new(titanium, 0, 3, 9);

        Assert.IsFalse(MapRoomMetadataAuthority.TryAccept(current, requested, out MapRoomMetadata accepted));
        Assert.AreSame(current, accepted);
    }

    [TestMethod]
    public void StaleRevisionIsRejected()
    {
        MapRoomMetadata current = new(quartz, 2, 4, 9);
        MapRoomMetadata requested = new(titanium, 0, 4, 8);

        Assert.IsFalse(MapRoomMetadataAuthority.TryAccept(current, requested, out MapRoomMetadata accepted));
        Assert.AreSame(current, accepted);
    }

    [TestMethod]
    public void DuplicateOrRegressedProgressIsRejected()
    {
        MapRoomMetadata current = new(quartz, 2, 4, 9);

        Assert.IsFalse(MapRoomMetadataAuthority.TryAccept(current, new MapRoomMetadata(quartz, 2, 4, 9), out _));
        Assert.IsFalse(MapRoomMetadataAuthority.TryAccept(current, new MapRoomMetadata(quartz, 1, 4, 9), out _));
    }

    [TestMethod]
    public void LegacyMetadataCanStartFirstGeneration()
    {
        MapRoomMetadata requested = new(quartz, 0);

        Assert.IsTrue(MapRoomMetadataAuthority.TryAccept(null, requested, out MapRoomMetadata accepted));
        Assert.AreEqual(1, accepted.Generation);
        Assert.AreEqual(1, accepted.Revision);
    }
}
