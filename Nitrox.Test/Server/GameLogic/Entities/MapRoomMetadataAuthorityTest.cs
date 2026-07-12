using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
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
    public void ProgressClassificationExcludesTargetChangesAndRegressions()
    {
        MapRoomMetadata current = new(quartz, 2, 4, 9);

        Assert.IsTrue(MapRoomMetadataAuthority.IsProgressUpdate(current, new MapRoomMetadata(quartz, 3, 4, 9)));
        Assert.IsFalse(MapRoomMetadataAuthority.IsProgressUpdate(current, new MapRoomMetadata(titanium, 3, 4, 9)));
        Assert.IsFalse(MapRoomMetadataAuthority.IsProgressUpdate(current, new MapRoomMetadata(quartz, 1, 4, 9)));
        Assert.IsFalse(MapRoomMetadataAuthority.IsProgressUpdate(null, new MapRoomMetadata(quartz, 1)));
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

    [TestMethod]
    public void AcceptedTargetChangeAtomicallyClearsOldResults()
    {
        MapRoomEntity room = CreateRoom(new MapRoomMetadata(quartz, 2, 3, 8));
        room.BeginScanResultGeneration(3);
        room.TryApplyScanResult(3, new MapRoomScanResultRecord("old", quartz, NitroxVector3.Zero));

        Assert.IsTrue(MapRoomMetadataAuthority.TryAcceptAndApply(room, new MapRoomMetadata(titanium, 2, 3, 8), out MapRoomMetadata accepted));

        Assert.AreSame(accepted, room.Metadata);
        Assert.AreEqual(4, room.ScanResultGeneration);
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void AcceptedProgressUpdateRetainsCurrentGenerationResults()
    {
        MapRoomEntity room = CreateRoom(new MapRoomMetadata(quartz, 2, 3, 8));
        room.BeginScanResultGeneration(3);
        room.TryApplyScanResult(3, new MapRoomScanResultRecord("current", quartz, NitroxVector3.Zero));

        Assert.IsTrue(MapRoomMetadataAuthority.TryAcceptAndApply(room, new MapRoomMetadata(quartz, 3, 3, 8), out _));

        Assert.AreEqual(3, room.ScanResultGeneration);
        Assert.AreEqual(1, room.ScanResults.Count);
    }

    private static MapRoomEntity CreateRoom(MapRoomMetadata metadata)
    {
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3());
        room.Metadata = metadata;
        return room;
    }
}
