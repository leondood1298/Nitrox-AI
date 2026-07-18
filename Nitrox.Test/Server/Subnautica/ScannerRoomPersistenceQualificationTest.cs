using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class ScannerRoomPersistenceQualificationTest
{
    private static readonly NitroxId BaseId = new("00000000-0000-0000-0000-000000000101");
    private static readonly NitroxId RoomId = new("00000000-0000-0000-0000-000000000100");
    private static readonly NitroxId CameraAId = new("00000000-0000-0000-0000-000000000201");
    private static readonly NitroxId CameraBId = new("00000000-0000-0000-0000-000000000202");
    private static readonly NitroxId CameraCId = new("00000000-0000-0000-0000-000000000203");

    [TestMethod]
    public void JsonRoundTripPreservesCanonicalStateAndQualifiesLateJoinRecovery()
    {
        QualifyPersistenceAndInitialSync(
            new ServerJsonSerializer(NullLogger<ServerJsonSerializer>.Instance),
            nameof(ServerJsonSerializer));
    }

    [TestMethod]
    public void ProtoBufRoundTripPreservesCanonicalStateAndQualifiesLateJoinRecovery()
    {
        QualifyPersistenceAndInitialSync(
            new SubnauticaServerProtoBufSerializer(NullLogger<SubnauticaServerProtoBufSerializer>.Instance),
            nameof(SubnauticaServerProtoBufSerializer));
    }

    private static void QualifyPersistenceAndInitialSync(IServerSerializer serializer, string serializerName)
    {
        MapRoomEntity originalRoom = CreateRoom();
        ScannerRoomStateSnapshot originalSnapshot = ScannerRoomStateFingerprint.Create(originalRoom);

        GlobalRootData restoredData = RoundTrip(serializer, CreatePersistedWorld(originalRoom), serializerName);
        MapRoomEntity restoredRoom = GetPersistedRoom(restoredData);

        AssertCanonicalFields(originalRoom, restoredRoom, $"{serializerName} round trip");
        AssertEquivalentFingerprint(originalSnapshot, ScannerRoomStateFingerprint.Create(restoredRoom),
            $"{serializerName} round trip");

        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        WorldEntityManager worldEntityManager = new(
            null!,
            entityRegistry,
            null!,
            null!,
            new MapRoomScanResultSubscriptions(),
            diagnostics,
            NullLogger<WorldEntityManager>.Instance);

        foreach (GlobalRootEntity root in restoredData.Entities)
        {
            worldEntityManager.AddOrUpdateGlobalRootEntity(root);
        }

        List<GlobalRootEntity> initialSync = worldEntityManager.GetInitialSyncGlobalRootEntities(rootOnly: true);

        AssertInitialSyncRoots(initialSync, serializerName);
        Assert.IsTrue(entityRegistry.TryGetEntityById(CameraAId, out GlobalRootEntity _),
            $"{serializerName}: docked camera A must be filtered because of dock state, not because it is absent.");
        Assert.IsFalse(entityRegistry.TryGetEntityById(CameraBId, out Entity _),
            $"{serializerName}: the orphaned camera must remain absent as a loose global entity.");
        Assert.IsTrue(entityRegistry.TryGetEntityById(CameraCId, out GlobalRootEntity _),
            $"{serializerName}: existing loose camera C must remain registered for initial sync.");

        MapRoomEntity expectedAfterRecovery = CreateRoom();
        expectedAfterRecovery.RightDockCameraId = CameraBId;
        expectedAfterRecovery.DockingRevision = 10;

        AssertCanonicalFields(expectedAfterRecovery, restoredRoom, $"{serializerName} initial sync recovery");
        ScannerRoomStateSnapshot recoveredSnapshot = ScannerRoomStateFingerprint.Create(restoredRoom);
        AssertEquivalentFingerprint(ScannerRoomStateFingerprint.Create(expectedAfterRecovery), recoveredSnapshot,
            $"{serializerName} initial sync recovery");
        CollectionAssert.AreEqual(
            new[] { 1, 2, 7 },
            restoredRoom.CameraRegistry.OrderBy(record => record.CameraNumber).Select(record => record.CameraNumber).ToArray(),
            $"{serializerName}: recovery must preserve stable camera registration numbers.");

        IReadOnlyList<ScannerRoomDiagnosticEntry> recoveryDiagnostics = diagnostics.GetHistory();
        Assert.AreEqual(1, recoveryDiagnostics.Count, $"{serializerName}: recovery should emit one bounded diagnostic.");
        Assert.AreEqual("orphan_recovery", recoveryDiagnostics[0].EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.InvariantFailure, recoveryDiagnostics[0].Outcome);
        Assert.AreEqual("restored_1", recoveryDiagnostics[0].Reason);

        List<GlobalRootEntity> repeatedInitialSync = worldEntityManager.GetInitialSyncGlobalRootEntities(rootOnly: true);
        AssertInitialSyncRoots(repeatedInitialSync, $"{serializerName} repeated sync");
        AssertEquivalentFingerprint(recoveredSnapshot, ScannerRoomStateFingerprint.Create(restoredRoom),
            $"{serializerName} repeated sync");
        Assert.AreEqual(1, diagnostics.GetHistory().Count,
            $"{serializerName}: idempotent initial sync must not emit duplicate recovery diagnostics.");
    }

    private static GlobalRootData RoundTrip(IServerSerializer serializer, GlobalRootData data, string serializerName)
    {
        byte[] payload;
        using (MemoryStream output = new())
        {
            serializer.Serialize(output, data);
            payload = output.ToArray();
        }

        Assert.IsTrue(payload.Length > 0, $"{serializerName} must produce a persistence payload.");
        using MemoryStream input = new(payload);
        GlobalRootData restored = serializer.Deserialize<GlobalRootData>(input);
        Assert.IsNotNull(restored, $"{serializerName} must deserialize the persisted global-root container.");
        return restored;
    }

    private static GlobalRootData CreatePersistedWorld(MapRoomEntity room)
    {
        GlobalRootEntity baseRoot = new(
            new NitroxTransform(new NitroxVector3(10f, -4f, 22f), NitroxQuaternion.Identity, NitroxVector3.One),
            GlobalRootEntity.GLOBAL_ROOT_LEVEL,
            "scanner-room-base-root",
            true,
            BaseId,
            new NitroxTechType("Base"),
            null,
            null,
            [room]);

        return GlobalRootData.From(
        [
            baseRoot,
            CreateLooseCamera(CameraAId, new NitroxVector3(11f, -3f, 23f)),
            CreateLooseCamera(CameraCId, new NitroxVector3(31f, 7f, -9f))
        ]);
    }

    private static MapRoomEntity CreateRoom() => new(
        new NitroxInt3(4, -2, 9),
        CameraAId,
        null,
        9,
        [
            new MapRoomCameraRecord(CameraAId, 1, true, 11, 90f, 80f, 12),
            new MapRoomCameraRecord(CameraBId, 2, false, 4, 44.5f, 55.25f, 6),
            new MapRoomCameraRecord(CameraCId, 7, true, 8, 22f, 33f, 9)
        ],
        31,
        44,
        [
            new MapRoomScanResultRecord("resource-b", new NitroxTechType("Silver"), new NitroxVector3(4f, 5f, 6f)),
            new MapRoomScanResultRecord("resource-a", new NitroxTechType("Gold"), new NitroxVector3(1f, 2f, 3f))
        ],
        52,
        [new NitroxTechType("Silver"), new NitroxTechType("Gold")],
        new CrafterMetadata(new NitroxTechType("MapRoomHUDChip"), 12.5f, 4f, 2, 1),
        new NitroxTransform(new NitroxVector3(1.25f, 2.5f, -3.75f), NitroxQuaternion.Identity, NitroxVector3.One),
        GlobalRootEntity.GLOBAL_ROOT_LEVEL,
        "scanner-room",
        true,
        RoomId,
        new NitroxTechType("MapRoom"),
        new MapRoomMetadata(new NitroxTechType("Gold"), 17, 31, 32),
        BaseId,
        []);

    private static GlobalRootEntity CreateLooseCamera(NitroxId id, NitroxVector3 position) => new(
        new NitroxTransform(position, NitroxQuaternion.Identity, NitroxVector3.One),
        GlobalRootEntity.GLOBAL_ROOT_LEVEL,
        "733fd479-0760-4bc2-a03e-281cbf02bfa4",
        true,
        id,
        new NitroxTechType("MapRoomCamera"),
        null,
        null,
        []);

    private static MapRoomEntity GetPersistedRoom(GlobalRootData data)
    {
        GlobalRootEntity baseRoot = data.Entities.Single(entity => entity.Id == BaseId);
        return baseRoot.ChildEntities.OfType<MapRoomEntity>().Single(entity => entity.Id == RoomId);
    }

    private static void AssertInitialSyncRoots(IEnumerable<GlobalRootEntity> roots, string context)
    {
        string[] expected = [BaseId.ToString(), CameraCId.ToString()];
        Array.Sort(expected, StringComparer.Ordinal);
        string[] actual = roots.Select(root => root.Id.ToString()).Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(expected, actual,
            $"{context}: initial sync must include the base and existing loose camera, while excluding docked cameras.");
    }

    private static void AssertCanonicalFields(MapRoomEntity expected, MapRoomEntity actual, string context)
    {
        Assert.AreEqual(expected.Id, actual.Id, $"{context}: room id");
        Assert.AreEqual(expected.ParentId, actual.ParentId, $"{context}: parent id");
        Assert.AreEqual(expected.Cell, actual.Cell, $"{context}: cell");
        Assert.AreEqual(expected.LeftDockCameraId, actual.LeftDockCameraId, $"{context}: left dock");
        Assert.AreEqual(expected.RightDockCameraId, actual.RightDockCameraId, $"{context}: right dock");
        Assert.AreEqual(expected.DockingRevision, actual.DockingRevision, $"{context}: docking revision");

        MapRoomCameraRecord[] expectedCameras = expected.CameraRegistry.OrderBy(record => record.CameraNumber).ToArray();
        MapRoomCameraRecord[] actualCameras = actual.CameraRegistry.OrderBy(record => record.CameraNumber).ToArray();
        Assert.AreEqual(expectedCameras.Length, actualCameras.Length, $"{context}: camera registration count");
        for (int i = 0; i < expectedCameras.Length; i++)
        {
            MapRoomCameraRecord expectedCamera = expectedCameras[i];
            MapRoomCameraRecord actualCamera = actualCameras[i];
            Assert.AreEqual(expectedCamera.CameraId, actualCamera.CameraId, $"{context}: camera {i} id");
            Assert.AreEqual(expectedCamera.CameraNumber, actualCamera.CameraNumber, $"{context}: camera {i} number");
            Assert.AreEqual(expectedCamera.LightOn, actualCamera.LightOn, $"{context}: camera {i} light");
            Assert.AreEqual(expectedCamera.LightRevision, actualCamera.LightRevision, $"{context}: camera {i} light revision");
            Assert.AreEqual(expectedCamera.Energy, actualCamera.Energy, $"{context}: camera {i} energy");
            Assert.AreEqual(expectedCamera.Health, actualCamera.Health, $"{context}: camera {i} health");
            Assert.AreEqual(expectedCamera.ComponentRevision, actualCamera.ComponentRevision,
                $"{context}: camera {i} component revision");
        }

        Assert.IsInstanceOfType<MapRoomMetadata>(expected.Metadata, $"{context}: expected scan metadata");
        Assert.IsInstanceOfType<MapRoomMetadata>(actual.Metadata, $"{context}: restored scan metadata");
        MapRoomMetadata expectedMetadata = (MapRoomMetadata)expected.Metadata;
        MapRoomMetadata actualMetadata = (MapRoomMetadata)actual.Metadata;
        Assert.AreEqual(expectedMetadata.TypeToScan.Name, actualMetadata.TypeToScan.Name, $"{context}: scan target");
        Assert.AreEqual(expectedMetadata.NumNodesScanned, actualMetadata.NumNodesScanned, $"{context}: scan progress");
        Assert.AreEqual(expectedMetadata.Generation, actualMetadata.Generation, $"{context}: metadata generation");
        Assert.AreEqual(expectedMetadata.Revision, actualMetadata.Revision, $"{context}: metadata revision");

        Assert.AreEqual(expected.ScanResultGeneration, actual.ScanResultGeneration, $"{context}: result generation");
        Assert.AreEqual(expected.ScanResultRevision, actual.ScanResultRevision, $"{context}: result revision");
        MapRoomScanResultRecord[] expectedResults = expected.ScanResults.OrderBy(result => result.ResourceId, StringComparer.Ordinal).ToArray();
        MapRoomScanResultRecord[] actualResults = actual.ScanResults.OrderBy(result => result.ResourceId, StringComparer.Ordinal).ToArray();
        Assert.AreEqual(expectedResults.Length, actualResults.Length, $"{context}: result count");
        for (int i = 0; i < expectedResults.Length; i++)
        {
            Assert.AreEqual(expectedResults[i].ResourceId, actualResults[i].ResourceId, $"{context}: result {i} resource");
            Assert.AreEqual(expectedResults[i].TechType.Name, actualResults[i].TechType.Name, $"{context}: result {i} type");
            Assert.AreEqual(expectedResults[i].Position, actualResults[i].Position, $"{context}: result {i} position");
        }

        Assert.AreEqual(expected.AvailableScanTypesRevision, actual.AvailableScanTypesRevision,
            $"{context}: available scan-types revision");
        CollectionAssert.AreEqual(
            expected.AvailableScanTypes.Select(type => type.Name).ToArray(),
            actual.AvailableScanTypes.Select(type => type.Name).ToArray(),
            $"{context}: available scan types");

        Assert.IsNotNull(expected.FabricatorMetadata, $"{context}: expected fabricator metadata");
        Assert.IsNotNull(actual.FabricatorMetadata, $"{context}: restored fabricator metadata");
        Assert.AreEqual(expected.FabricatorMetadata.TechType.Name, actual.FabricatorMetadata.TechType.Name,
            $"{context}: fabricator type");
        Assert.AreEqual(expected.FabricatorMetadata.StartTime, actual.FabricatorMetadata.StartTime,
            $"{context}: fabricator start time");
        Assert.AreEqual(expected.FabricatorMetadata.Duration, actual.FabricatorMetadata.Duration,
            $"{context}: fabricator duration");
        Assert.AreEqual(expected.FabricatorMetadata.Amount, actual.FabricatorMetadata.Amount,
            $"{context}: fabricator amount");
        Assert.AreEqual(expected.FabricatorMetadata.LinkedIndex, actual.FabricatorMetadata.LinkedIndex,
            $"{context}: fabricator linked index");
    }

    private static void AssertEquivalentFingerprint(
        ScannerRoomStateSnapshot expected,
        ScannerRoomStateSnapshot actual,
        string context)
    {
        Assert.AreEqual(expected.CanonicalState, actual.CanonicalState, $"{context}: canonical persisted state");
        Assert.AreEqual(expected.Fingerprint, actual.Fingerprint, $"{context}: canonical state fingerprint");
    }
}
