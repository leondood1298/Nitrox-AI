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
using Nitrox.Test.Server.Subnautica;

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
        accepted.Format().Should().Be($"[SRD1] n=1 ep={diagnostics.Epoch} side=S sid=2 ev=dock out=ok room=11111111 cam=33333333.3333 slot=1 dockRev=7 cams=2 roomLock=? camLock=? fp=- reason=owner_transfer");
        rejected.Format().Should().StartWith($"[SRD1] n=2 ep={diagnostics.Epoch} side=S sid=3 ev=control_request out=reject room=11111111 ");
        rejected.Format().Should().EndWith(" reason=not_owner_retry");
        rejected.Format().Should().NotContain("\r").And.NotContain("\n");
        accepted.StateFingerprint.Should().BeNull("transition logging must stay O(1) even with large scan result sets");

        logger.Entries.Should().HaveCount(3, "the first event of a run is preceded by its epoch marker");
        logger.Entries[1].Should().Be((LogLevel.Information, accepted.Format()));
        logger.Entries[2].Should().Be((LogLevel.Warning, rejected.Format()));
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(2, 1, 1, 0, 0));
    }

    [TestMethod]
    public void FirstEventIsPrecededByOneEpochMarkerWithUtcTime()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ManualTimeProvider time = new(new DateTimeOffset(2026, 11, 23, 15, 4, 5, TimeSpan.FromHours(1)));
        ScannerRoomDiagnostics diagnostics = new(logger, timeProvider: time);

        diagnostics.RecordAccepted("dock");
        diagnostics.RecordAccepted("undock");

        logger.Entries.Should().HaveCount(3);
        logger.Entries[0].Should().Be((LogLevel.Information,
            $"[SRD1] n=- ep={diagnostics.Epoch} side=S sid=- ev=epoch out=info utc=2026-11-23T14:04:05Z reason=server_diagnostics_started"));
        diagnostics.GetCounters().Recorded.Should().Be(2, "a marker is not an event and must not leave a gap in the numbering");
        diagnostics.GetHistory().Should().HaveCount(2);
    }

    [TestMethod]
    public void EveryLineNamesWhoSimulatesTheRoomAndTheCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance, scenario.Ownership);
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;

        string unowned = diagnostics.RecordRejected("scan_snapshot", scenario.Room, cameraId, scenario.PlayerB.SessionId, reason: "non_owner").Format();
        scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT).Should().BeTrue();
        scenario.Ownership.TryAcquire(cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE).Should().BeTrue();
        string owned = diagnostics.RecordAccepted("control_acquire", scenario.Room, cameraId, scenario.PlayerB.SessionId).Format();
        string withoutSubject = diagnostics.RecordAccepted("camera_lock", sessionId: scenario.PlayerB.SessionId).Format();

        unowned.Should().Contain(" roomLock=none camLock=none ", "a room or camera nobody simulates is the cause of most rejections");
        owned.Should().Contain(" sid=2 ").And.Contain(" roomLock=1t camLock=2x ");
        withoutSubject.Should().Contain(" room=- cam=- ").And.Contain(" roomLock=- camLock=- ");
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
    public void RejectionsAreLoggedAgainAfterABurstHasPassed()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        ScannerRoomDiagnostics diagnostics = new(logger, timeProvider: time);
        for (int index = 0; index < 300; index++)
        {
            diagnostics.RecordRejected("control", cameraId: new NitroxId("55555555-5555-5555-5555-555555555555"),
                sessionId: (SessionId)(ushort)9, reason: $"burst_{index}");
        }

        time.Advance(TimeSpan.FromMinutes(2));
        ScannerRoomDiagnosticEntry later = diagnostics.RecordRejected("dock", cameraId: new NitroxId("66666666-6666-6666-6666-666666666666"),
            sessionId: (SessionId)(ushort)3, reason: "non_owner");

        logger.Entries.Should().Contain((LogLevel.Warning, later.Format()),
            "a rejection minutes after a burst is exactly the line a tester is looking for");
    }

    [TestMethod]
    public void MutedRejectionsAreAnnouncedAndSummedUpInTheLog()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        ScannerRoomDiagnostics diagnostics = new(logger, timeProvider: time);

        for (int index = 0; index < 200; index++)
        {
            diagnostics.RecordRejected("control", sessionId: (SessionId)(ushort)9, reason: $"distinct_{index}");
        }
        time.Advance(TimeSpan.FromMinutes(2));
        diagnostics.RecordAccepted("dock");

        string[] lines = logger.Entries.Select(entry => entry.Message).ToArray();
        lines.Count(line => line.Contains(" out=reject ")).Should().Be(128);
        lines.Should().ContainSingle(line => line.Contains(" ev=log_budget ") && line.EndsWith(" reason=rejections_muted_after_128_lines_in_60s"),
            "a reader must be able to tell that lines are missing from here on");
        lines.Should().ContainSingle(line => line.Contains(" ev=log_budget ") && line.EndsWith(" reason=rejections_logged_again_72_were_muted"));
        diagnostics.GetCounters().Rejected.Should().Be(200);
        diagnostics.GetCounters().SuppressedWarnings.Should().Be(72);
    }

    [TestMethod]
    public void RepeatedRejectionSaysHowOftenItHappened()
    {
        CapturingLogger<ScannerRoomDiagnostics> logger = new();
        ScannerRoomDiagnostics diagnostics = new(logger);

        for (int index = 0; index < 9; index++)
        {
            diagnostics.RecordRejected("component", sessionId: (SessionId)(ushort)2, reason: "non_owner");
        }

        string[] rejections = logger.Entries.Select(entry => entry.Message).Where(line => line.Contains(" out=reject ")).ToArray();
        rejections.Should().HaveCount(4, "the first two and then every power of two are written");
        rejections[0].Should().EndWith(" fp=- reason=non_owner");
        rejections[1].Should().EndWith(" fp=- rep=2 reason=non_owner");
        rejections[3].Should().EndWith(" fp=- rep=8 reason=non_owner");
    }

    [TestMethod]
    public void FailingLoggerCannotBreakTheCodeBeingObserved()
    {
        ScannerRoomDiagnostics diagnostics = new(new ThrowingLogger());
        MapRoomEntity room = CreateRoom();

        Action record = () =>
        {
            diagnostics.RecordAccepted("dock", room);
            diagnostics.RecordRejected("dock", room, reason: "non_owner");
            diagnostics.RecordInvariantFailure("control_revoke", room, reason: "duplicate_registration");
            diagnostics.RecordCheckpoint("manual", room);
        };

        record.Should().NotThrow("a packet processor must finish its work whatever happens to its diagnostics");
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(4, 1, 1, 1, 1));
    }

    [TestMethod]
    public void UnreadableRoomCannotBreakTheCodeBeingObserved()
    {
        ScannerRoomDiagnostics diagnostics = new(NullLogger<ScannerRoomDiagnostics>.Instance);
        MapRoomEntity room = CreateRoom();
        room.CameraRegistry = null!;
        room.ScanResults = null!;

        Action record = () =>
        {
            diagnostics.RecordAccepted("dock", room);
            diagnostics.RecordCheckpoint("manual", room);
        };

        record.Should().NotThrow();
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

        entry.Outcome.Should().Be(ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE);
        entry.Reason.Should().Be("duplicate_dock");
        entry.StateFingerprint.Should().NotBeNull();
        diagnostics.GetCounters().Should().Be(new ScannerRoomDiagnosticCounters(1, 0, 0, 1, 0));
    }

    [TestMethod]
    public void ValidationNamesAnEmptyRegistrySlotAsWhatItIs()
    {
        MapRoomEntity room = CreateRoom();
        room.CameraRegistry.Add(null!);

        ScannerRoomStateFingerprint.Validate(room).Should().Be("null_camera_record",
            "the cameras that are registered are all distinct; sending a reader after a duplicate would waste their time");

        MapRoomEntity duplicated = CreateRoom();
        duplicated.CameraRegistry.Add(new MapRoomCameraRecord(duplicated.CameraRegistry[0].CameraId, 3));
        ScannerRoomStateFingerprint.Validate(duplicated).Should().Be("duplicate_camera_registration");
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
        history.Should().HaveCount(ScannerRoomDiagnostics.HISTORY_CAPACITY);
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
        history.Should().HaveCount(ScannerRoomDiagnostics.HISTORY_CAPACITY);
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
        services.Single(descriptor => descriptor.ServiceType == typeof(ScannerRoomManager)).Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [TestMethod]
    public void ServiceContainerGivesDiagnosticsTheLockTable()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWorld();
        using ServiceProvider provider = services.BuildServiceProvider();

        ScannerRoomDiagnostics diagnostics = provider.GetRequiredService<ScannerRoomDiagnostics>();

        diagnostics.DescribeLock(new NitroxId()).Should().Be(ScannerRoomDiagnostics.NO_LOCK,
            "without the lock table every line would only say that the simulating player is unknown");
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

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => now;

        public override long GetTimestamp() => now.UtcTicks;

        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class ThrowingLogger : ILogger<ScannerRoomDiagnostics>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("log sink is gone");
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
