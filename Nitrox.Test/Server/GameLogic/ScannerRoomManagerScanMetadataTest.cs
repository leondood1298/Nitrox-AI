using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Which requests for a room's scan target and scan progress are accepted. What the server sends in answer is
///     tested with the metadata update processor.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerScanMetadataTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    [TestMethod]
    public void TargetChangeAdvancesGenerationAndResetsProgress()
    {
        MapRoomMetadata current = new(quartz, 8, 3);
        MapRoomMetadata requested = new(titanium, 8, 3);

        bool result = ScannerRoomManager.TryAcceptScanMetadata(current, requested, out MapRoomMetadata accepted);

        Assert.IsTrue(result);
        Assert.AreEqual(titanium, accepted.TypeToScan);
        Assert.AreEqual(0, accepted.NumNodesScanned);
        Assert.AreEqual(4, accepted.Generation);
    }

    [TestMethod]
    public void ProgressKeepsTheGeneration()
    {
        MapRoomMetadata current = new(quartz, 2, 4);
        MapRoomMetadata requested = new(quartz, 3, 4);

        bool result = ScannerRoomManager.TryAcceptScanMetadata(current, requested, out MapRoomMetadata accepted);

        Assert.IsTrue(result);
        Assert.AreEqual(quartz, accepted.TypeToScan);
        Assert.AreEqual(3, accepted.NumNodesScanned);
        Assert.AreEqual(4, accepted.Generation);
    }

    [TestMethod]
    public void ProgressClassificationExcludesTargetChangesAndRegressions()
    {
        MapRoomMetadata current = new(quartz, 2, 4);

        Assert.IsTrue(ScannerRoomManager.IsScanProgress(current, new MapRoomMetadata(quartz, 3, 4)));
        Assert.IsFalse(ScannerRoomManager.IsScanProgress(current, new MapRoomMetadata(titanium, 3, 4)));
        Assert.IsFalse(ScannerRoomManager.IsScanProgress(current, new MapRoomMetadata(quartz, 1, 4)));
        Assert.IsFalse(ScannerRoomManager.IsScanProgress(null, new MapRoomMetadata(quartz, 1)));
    }

    [TestMethod]
    public void RequestMadeAgainstAnotherGenerationIsRejected()
    {
        MapRoomMetadata current = new(quartz, 2, 4);

        Assert.IsFalse(ScannerRoomManager.TryAcceptScanMetadata(current, new MapRoomMetadata(titanium, 0, 3), out MapRoomMetadata accepted));
        Assert.AreSame(current, accepted);
        Assert.IsFalse(ScannerRoomManager.TryAcceptScanMetadata(current, new MapRoomMetadata(quartz, 9, 3), out _), "Progress of the scan before this one.");
        Assert.IsFalse(ScannerRoomManager.TryAcceptScanMetadata(current, new MapRoomMetadata(titanium, 0, 5), out _), "The server alone counts the generations.");
    }

    [TestMethod]
    public void DuplicateOrRegressedProgressIsRejected()
    {
        MapRoomMetadata current = new(quartz, 2, 4);

        Assert.IsFalse(ScannerRoomManager.TryAcceptScanMetadata(current, new MapRoomMetadata(quartz, 2, 4), out _));
        Assert.IsFalse(ScannerRoomManager.TryAcceptScanMetadata(current, new MapRoomMetadata(quartz, 1, 4), out _));
    }

    [TestMethod]
    public void RoomThatNeverScannedCanStartItsFirstGeneration()
    {
        MapRoomMetadata requested = new(quartz, 0);

        Assert.IsTrue(ScannerRoomManager.TryAcceptScanMetadata(null, requested, out MapRoomMetadata accepted));
        Assert.AreEqual(1, accepted.Generation);
    }

    [TestMethod]
    public void AcceptedRequestIsStoredInTheRoomAndARefusedOneIsNot()
    {
        MapRoomMetadata stored = new(quartz, 2, 3);
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3()) { Metadata = stored };

        Assert.IsFalse(ScannerRoomManager.TryApplyScanMetadata(room, new MapRoomMetadata(titanium, 0, 2), out _));
        Assert.AreSame(stored, room.Metadata);
        Assert.IsTrue(ScannerRoomManager.TryApplyScanMetadata(room, new MapRoomMetadata(titanium, 2, 3), out MapRoomMetadata accepted));

        Assert.AreSame(accepted, room.Metadata);
        Assert.AreEqual(4, accepted.Generation);
    }
}
