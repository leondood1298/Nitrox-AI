using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Extensions;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class ScannerRoomDiagnosticsTest
{
    [TestMethod]
    public void EmitsStableSingleLineFormatAndTracksOutcomes()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ScannerRoomDiagnostics diagnostics = new(logger);
        MapRoomEntity room = CreateRoom();
        NitroxId cameraId = room.CameraRegistry[0].CameraId;

        ScannerRoomDiagnosticEntry accepted = diagnostics.RecordAccepted("Dock", room, cameraId, (SessionId)(ushort)2, 1, "owner transfer");
        ScannerRoomDiagnosticEntry rejected = diagnostics.RecordRejected("Control\nRequest", room, cameraId, (SessionId)(ushort)3, reason: "not owner\r\nretry");

        diagnostics.Epoch.Should().MatchRegex("^[0-9a-f]{8}$");
        accepted.Format().Should().StartWith($"[SRD1] n=1 ep={diagnostics.Epoch} side=S ev=dock out=ok sid=2 room=11111111 cam=33333333.3333 slot=1 dRev=7 cams=2 fp=");
        accepted.Format().Should().EndWith(" reason=owner_transfer");
        rejected.Format().Should().Contain($"n=2 ep={diagnostics.Epoch} side=S ev=control_request out=reject sid=3");
        rejected.Format().Should().EndWith(" reason=not_owner_retry");
        rejected.Format().Should().NotContain("\r").And.NotContain("\n");
        accepted.StateFingerprint.Should().BeNull("transition logging must stay O(1) even with large scan result sets");

        logger.Entries.Should().HaveCount(2);
        logger.Entries[0].Should().Be((LogLevel.Information, accepted.Format()));
        logger.Entries[1].Should().Be((LogLevel.Warning, rejected.Format()));
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(2, 1, 1, 0, 0));
    }

    [TestMethod]
    public void EpochIsStableWithinInstanceAndDistinctAcrossInstances()
    {
        ScannerRoomDiagnostics first = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        ScannerRoomDiagnostics second = new(NullLogger<ScannerRoomDiagnostics>.Instance);

        ScannerRoomDiagnosticEntry firstEntry = first.RecordAccepted("dock");
        ScannerRoomDiagnosticEntry nextFirstEntry = first.RecordAccepted("undock");
        ScannerRoomDiagnosticEntry secondEntry = second.RecordAccepted("dock");

        first.Epoch.Should().MatchRegex("^[0-9a-f]{8}$");
        second.Epoch.Should().MatchRegex("^[0-9a-f]{8}$");
        firstEntry.Epoch.Should().Be(first.Epoch).And.Be(nextFirstEntry.Epoch);
        secondEntry.Epoch.Should().Be(second.Epoch);
        second.Epoch.Should().NotBe(first.Epoch, "epochs use an atomic per-process instance sequence");
    }

    [TestMethod]
    public void DuplicateWarningsAreSampledButEveryRejectionIsCounted()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ScannerRoomDiagnostics diagnostics = new(logger);

        for (int index = 0; index < 300; index++)
        {
            diagnostics.RecordRejected("control", cameraId: new NitroxId("55555555-5555-5555-5555-555555555555"),
                sessionId: (SessionId)(ushort)9, reason: "non_owner");
        }

        ScannerRoomDiagnosticCounters counters = diagnostics.GetCounters();
        counters.Recorded.Should().Be(300);
        counters.Rejected.Should().Be(300);
        counters.SuppressedWarnings.Should().BeGreaterThan(280);
        logger.Entries.Should().HaveCountLessThan(20);
    }

    [TestMethod]
    public void CompactIdsDistinguishSiblingScannerCameras()
    {
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);

        string first = diagnostics.RecordAccepted("camera", cameraId: new NitroxId("99e1d312-b017-474e-8c30-ffc66573465f")).Format();
        string second = diagnostics.RecordAccepted("camera", cameraId: new NitroxId("99e1d312-b017-474e-72cf-00396573465f")).Format();

        first.Should().Contain("cam=99e1d312.8c30");
        second.Should().Contain("cam=99e1d312.72cf");
    }

    [TestMethod]
    public void InvariantAfterRejectionBurstIsNeverSuppressed()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ScannerRoomDiagnostics diagnostics = new(logger);

        for (int index = 0; index < 300; index++)
        {
            diagnostics.RecordRejected("control", cameraId: new NitroxId("55555555-5555-5555-5555-555555555555"),
                sessionId: (SessionId)(ushort)9, reason: "non_owner");
        }
        int warningCountBeforeInvariant = logger.Entries.Count;
        long suppressedBeforeInvariant = diagnostics.GetCounters().SuppressedWarnings;

        ScannerRoomDiagnosticEntry invariant = diagnostics.RecordInvariantFailure("control_revoke", reason: "duplicate_registration");

        logger.Entries.Should().HaveCount(warningCountBeforeInvariant + 1);
        logger.Entries[^1].Should().Be((LogLevel.Warning, invariant.Format()));
        diagnostics.GetCounters().InvariantFailures.Should().Be(1);
        diagnostics.GetCounters().SuppressedWarnings.Should().Be(suppressedBeforeInvariant);
    }

    [TestMethod]
    public void InvalidCheckpointIsRecordedAsInvariantInsteadOfCheckpoint()
    {
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        MapRoomEntity room = CreateRoom();
        room.RightDockCameraId = room.LeftDockCameraId;

        ScannerRoomDiagnosticEntry entry = diagnostics.RecordCheckpoint("manual", room);

        entry.Outcome.Should().Be(ScannerRoomDiagnosticOutcome.InvariantFailure);
        entry.Reason.Should().Be("duplicate_dock");
        entry.StateFingerprint.Should().NotBeNull();
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(1, 0, 0, 1, 0));
    }

    [TestMethod]
    public void RetainsOnlyNewest256EntriesInSequenceOrder()
    {
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);

        for (int i = 0; i < 300; i++)
        {
            diagnostics.RecordAccepted("dock");
        }

        IReadOnlyList<ScannerRoomDiagnosticEntry> history = diagnostics.GetHistory();
        history.Should().HaveCount(ScannerRoomDiagnostics.HistoryCapacity);
        history[0].Sequence.Should().Be(45);
        history[^1].Sequence.Should().Be(300);
        history.Select(entry => entry.Sequence).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(300, 300, 0, 0, 0));
    }

    [TestMethod]
    public void ConcurrentRecordingProducesUniqueOrderedSequence()
    {
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);

        Parallel.For(0, 1000, _ => diagnostics.RecordCheckpoint("fixture", CreateRoom()));

        IReadOnlyList<ScannerRoomDiagnosticEntry> history = diagnostics.GetHistory();
        history.Should().HaveCount(ScannerRoomDiagnostics.HistoryCapacity);
        history.Select(entry => entry.Sequence).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(1000, 0, 0, 0, 1000));
    }

    [TestMethod]
    public void FingerprintIsOrderIndependentAndCoversPersistedScannerState()
    {
        MapRoomEntity first = CreateRoom();
        MapRoomEntity reordered = CreateRoom();
        reordered.CameraRegistry.Reverse();
        reordered.ScanResults.Reverse();
        reordered.AvailableScanTypes.Reverse();

        ScannerRoomStateFingerprint.Create(first).Fingerprint.Should().Be(ScannerRoomStateFingerprint.Create(reordered).Fingerprint);

        reordered.CameraRegistry.Single(camera => camera.CameraId.ToString().StartsWith("33333333", StringComparison.Ordinal)).Energy = 12.5f;
        reordered.CameraRegistry.Single(camera => camera.CameraId.ToString().StartsWith("33333333", StringComparison.Ordinal)).ComponentRevision++;

        ScannerRoomStateFingerprint.Create(first).Fingerprint.Should().NotBe(ScannerRoomStateFingerprint.Create(reordered).Fingerprint);

        MapRoomEntity metadataChanged = CreateRoom();
        metadataChanged.Metadata = new MapRoomMetadata(new NitroxTechType("Quartz"), 8, 10, 13);
        ScannerRoomStateFingerprint.Create(first).Fingerprint.Should().NotBe(ScannerRoomStateFingerprint.Create(metadataChanged).Fingerprint);

        MapRoomEntity crafterChanged = CreateRoom();
        crafterChanged.FabricatorMetadata = new CrafterMetadata(new NitroxTechType("MapRoomRangeUpgrade"), 12.5f, 4f, 2, 1);
        ScannerRoomStateFingerprint.Create(first).Fingerprint.Should().NotBe(ScannerRoomStateFingerprint.Create(crafterChanged).Fingerprint);
    }

    [TestMethod]
    public void CanonicalStateUsesInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            MapRoomEntity room = CreateRoom();
            room.CameraRegistry[0].Energy = 12.5f;
            room.ScanResults[0].Position = new NitroxVector3(1.25f, 2.5f, 3.75f);

            string canonical = ScannerRoomStateFingerprint.Create(room).CanonicalState;

            canonical.Should().Contain("12.5").And.Contain("1.25").And.NotContain("12,5").And.NotContain("1,25");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestMethod]
    public void WorldRegistrationIncludesSingletonDiagnostics()
    {
        ServiceCollection services = new();

        services.AddWorld();

        services.Count(descriptor => descriptor.ServiceType == typeof(ScannerRoomDiagnostics)).Should().Be(1);
        services.Single(descriptor => descriptor.ServiceType == typeof(ScannerRoomDiagnostics)).Lifetime.Should().Be(ServiceLifetime.Singleton);
        services.Single(descriptor => descriptor.ServiceType == typeof(MapRoomCameraControlLifecycle)).Lifetime.Should().Be(ServiceLifetime.Singleton);
        services.Single(descriptor => descriptor.ServiceType == typeof(MapRoomCameraControlReleaseFactory)).Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    private static MapRoomEntity CreateRoom()
    {
        NitroxId roomId = new("11111111-1111-1111-1111-111111111111");
        NitroxId parentId = new("22222222-2222-2222-2222-222222222222");
        NitroxId firstCameraId = new("33333333-3333-3333-3333-333333333333");
        NitroxId secondCameraId = new("44444444-4444-4444-4444-444444444444");
        return new MapRoomEntity(roomId, parentId, new NitroxInt3(1, 2, 3))
        {
            Metadata = new MapRoomMetadata(new NitroxTechType("Gold"), 7, 9, 12),
            LeftDockCameraId = firstCameraId,
            RightDockCameraId = secondCameraId,
            DockingRevision = 7,
            CameraRegistry =
            [
                new MapRoomCameraRecord(firstCameraId, 1, true, 4, 80f, 90f, 6),
                new MapRoomCameraRecord(secondCameraId, 2, false, 3, 70f, 85f, 5)
            ],
            ScanResultGeneration = 9,
            ScanResultRevision = 10,
            ScanResults =
            [
                new MapRoomScanResultRecord("resource-b", new NitroxTechType("Silver"), new NitroxVector3(4f, 5f, 6f)),
                new MapRoomScanResultRecord("resource-a", new NitroxTechType("Gold"), new NitroxVector3(1f, 2f, 3f))
            ],
            AvailableScanTypesRevision = 11,
            AvailableScanTypes = [new NitroxTechType("Silver"), new NitroxTechType("Gold")],
            FabricatorMetadata = new CrafterMetadata(new NitroxTechType("MapRoomHUDChip"), 10f, 2f, 1, 0)
        };
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
