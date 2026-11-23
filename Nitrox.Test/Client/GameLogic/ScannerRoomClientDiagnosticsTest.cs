using Autofac;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class ScannerRoomClientDiagnosticsTest
{
    [TestMethod]
    public void ProducesBoundedStableSingleLineTrace()
    {
        ScannerRoomClientDiagnostics diagnostics = new();
        NitroxId cameraId = new("11111111-1111-1111-1111-111111111111");

        string first = diagnostics.Record("Dock Apply", "ok", cameraId, revision: 4, slot: 1, reason: "state applied\r\n");
        for (int i = 0; i < 150; i++)
        {
            diagnostics.Record("fixture", "ok");
        }

        StringAssert.Matches(diagnostics.Epoch, new System.Text.RegularExpressions.Regex("^[0-9a-f]{8}$"));
        Assert.AreEqual($"[SRD1] n=1 ep={diagnostics.Epoch} side=C sid=- ev=dock_apply out=ok room=- cam=11111111.1111 slot=1 rev=4 reason=state_applied", first);
        Assert.AreEqual(ScannerRoomClientDiagnostics.HISTORY_CAPACITY, diagnostics.GetHistory().Count);
        Assert.IsTrue(diagnostics.GetHistory()[0].Contains(" n=24 "));
        Assert.IsFalse(diagnostics.GetHistory().Any(line => line.Contains('\r') || line.Contains('\n')));
    }

    [TestMethod]
    public void LinesCarryTheSessionIdTheServerKnowsThisPlayerBy()
    {
        SessionId? sessionId = null;
        ScannerRoomClientDiagnostics diagnostics = new(() => sessionId, static () => DateTimeOffset.UnixEpoch);

        string beforeReservation = diagnostics.Record("dock_apply", "ok");
        sessionId = 7;
        string afterReservation = diagnostics.Record("dock_apply", "ok");

        StringAssert.Contains(beforeReservation, " side=C sid=- ev=dock_apply ");
        StringAssert.Contains(afterReservation, " side=C sid=7 ev=dock_apply ");
    }

    [TestMethod]
    public void ServiceContainerGivesDiagnosticsTheSession()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation((SessionId)(ushort)5));
        ContainerBuilder builder = new();
        builder.RegisterInstance(multiplayerSession).As<IMultiplayerSession>();
        builder.RegisterType<ScannerRoomClientDiagnostics>().InstancePerLifetimeScope();
        builder.RegisterType<BasePowerClientDiagnostics>().InstancePerLifetimeScope();
        using IContainer container = builder.Build();

        string scannerLine = container.Resolve<ScannerRoomClientDiagnostics>().Record("dock_apply", "ok");
        string powerLine = container.Resolve<BasePowerClientDiagnostics>().RecordAudioTransition("audio_up", false, null, 1f, 2f, true, false, null)!;

        StringAssert.Contains(scannerLine, " side=C sid=5 ");
        StringAssert.Contains(powerLine, " side=C sid=5 ");
    }

    [TestMethod]
    public void FailingSessionLookupCannotBreakTheCodeBeingObserved()
    {
        ScannerRoomClientDiagnostics diagnostics = new(static () => throw new InvalidOperationException("session is gone"),
            static () => throw new InvalidOperationException("clock is gone"));
        NitroxId cameraId = new("11111111-1111-1111-1111-111111111111");

        string line = diagnostics.Record("dock_apply", "ok", cameraId);
        diagnostics.RecordComponentApplied(cameraId, 50f, 50f, 1, true);

        StringAssert.Contains(line, " side=C sid=- ev=dock_apply out=ok ");
        Assert.AreEqual(2, diagnostics.GetHistory().Count);
    }

    [TestMethod]
    public void RepeatsOfAnythingButASuccessAreThinnedOut()
    {
        ScannerRoomClientDiagnostics diagnostics = new();
        NitroxId cameraId = new("11111111-1111-1111-1111-111111111111");

        for (int index = 0; index < 9; index++)
        {
            diagnostics.Record("component_apply", "reject", cameraId, revision: index, reason: "server_reject");
            diagnostics.Record("dock_apply", "ok", cameraId, revision: index);
        }

        string[] rejections = diagnostics.GetHistory().Where(line => line.Contains(" out=reject ")).ToArray();
        Assert.AreEqual(4, rejections.Length, "the first two and then every power of two are written");
        Assert.IsTrue(rejections[0].EndsWith(" rev=0 reason=server_reject"));
        Assert.IsTrue(rejections[3].EndsWith(" rev=7 rep=8 reason=server_reject"));
        Assert.AreEqual(9, diagnostics.GetHistory().Count(line => line.Contains(" ev=dock_apply out=ok ")), "a transition that went through is never dropped");
    }

    [TestMethod]
    public void PacketsAboutToBeIgnoredAreRecorded()
    {
        ScannerRoomClientDiagnostics diagnostics = new();
        NitroxId cameraId = new("11111111-1111-1111-1111-111111111111");
        Dictionary<NitroxId, long> appliedRevisions = new() { [cameraId] = 5 };

        diagnostics.RecordIfIgnored("light_apply", cameraId, isServerResponse: true, granted: false, revision: 0, appliedRevisions);
        diagnostics.RecordIfIgnored("light_apply", cameraId, isServerResponse: true, granted: true, revision: 3, appliedRevisions);
        diagnostics.RecordIfIgnored("light_apply", cameraId, isServerResponse: true, granted: true, revision: 5, appliedRevisions);
        diagnostics.RecordIfIgnored("light_apply", cameraId, isServerResponse: true, granted: true, revision: 6, appliedRevisions);
        diagnostics.RecordIfIgnored("light_apply", new NitroxId(), isServerResponse: true, granted: true, revision: 1, appliedRevisions);
        diagnostics.RecordIfIgnored("light_apply", cameraId, isServerResponse: false, granted: false, revision: 0, appliedRevisions);

        IReadOnlyList<string> history = diagnostics.GetHistory();
        Assert.AreEqual(2, history.Count, "packets that will be applied, and this client's own requests, are not ignored packets");
        StringAssert.Contains(history[0], " ev=light_apply out=reject room=- cam=11111111.1111 slot=- rev=0 reason=server_reject");
        StringAssert.Contains(history[1], " ev=light_apply out=stale room=- cam=11111111.1111 slot=- rev=3 reason=applied_5");
    }

    [TestMethod]
    public void CameraManagerNamesThePlayerWhoHoldsACamera()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation((SessionId)(ushort)1));
        ScannerRoomClientDiagnostics diagnostics = new();
        MapRoomCameras cameras = new(multiplayerSession, multiplayerSession, null!, new SimulationOwnership(multiplayerSession, multiplayerSession),
            diagnostics, new StalkerCameraLockPurposeTracker());
        NitroxId cameraId = new("11111111-1111-1111-1111-111111111111");

        // This client asked for a camera that session 2 is piloting, then session 2 lets go of it.
        cameras.ProcessControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, true, false, true, false, (SessionId)(ushort)2));
        cameras.ProcessControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, false, false, true, true, (SessionId)(ushort)2));

        IReadOnlyList<string> history = diagnostics.GetHistory();
        Assert.AreEqual(2, history.Count);
        Assert.IsTrue(history[0].Contains(" ev=control_apply out=reject ") && history[0].EndsWith(" reason=locked_by_2"));
        Assert.IsTrue(history[1].Contains(" ev=control_release_apply out=ok ") && history[1].EndsWith(" reason=remote_pilot_2"));
    }

    [TestMethod]
    public void CameraManagerRecordsRefusedAndOutdatedCameraState()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation((SessionId)(ushort)1));
        ScannerRoomClientDiagnostics diagnostics = new();
        MapRoomCameras cameras = new(multiplayerSession, multiplayerSession, null!, new SimulationOwnership(multiplayerSession, multiplayerSession),
            diagnostics, new StalkerCameraLockPurposeTracker());
        NitroxId cameraId = new("11111111-1111-1111-1111-111111111111");

        // The server refuses what this client reported, for instance a recharge from a player who does not simulate the room.
        cameras.ProcessComponentState(new MapRoomCameraComponentState(cameraId, 50f, 50f, 0, true, false));
        cameras.ProcessLight(new MapRoomCameraLight(cameraId, true, 0, true, false));

        IReadOnlyList<string> history = diagnostics.GetHistory();
        Assert.AreEqual(2, history.Count);
        StringAssert.Contains(history[0], " ev=component_apply out=reject ");
        StringAssert.Contains(history[1], " ev=light_apply out=reject ");
    }

    [TestMethod]
    public void EpochIsStableWithinInstanceAndDistinctAcrossInstances()
    {
        ScannerRoomClientDiagnostics first = new();
        ScannerRoomClientDiagnostics second = new();

        string firstLine = first.Record("dock_apply", "ok");
        string nextFirstLine = first.Record("undock_apply", "ok");
        string secondLine = second.Record("dock_apply", "ok");

        Assert.AreNotEqual(first.Epoch, second.Epoch, "epochs use an atomic per-process instance sequence");
        StringAssert.Contains(firstLine, $" ep={first.Epoch} ");
        StringAssert.Contains(nextFirstLine, $" ep={first.Epoch} ");
        StringAssert.Contains(secondLine, $" ep={second.Epoch} ");
    }

    [TestMethod]
    public void ComponentBandsAvoidUnchangedPacketSpam()
    {
        ScannerRoomClientDiagnostics diagnostics = new();
        NitroxId cameraId = new();

        diagnostics.RecordComponentApplied(cameraId, 100f, MapRoomCameraMetadata.MAX_HEALTH, 1, true);
        diagnostics.RecordComponentApplied(cameraId, 99f, 399f, 2, true);
        diagnostics.RecordComponentApplied(cameraId, 75f, 399f, 3, true);
        diagnostics.RecordComponentApplied(cameraId, 74f, 300f, 4, true);

        Assert.AreEqual(3, diagnostics.GetHistory().Count);
        Assert.IsTrue(diagnostics.GetHistory()[0].EndsWith("reason=e100_h100"));
        Assert.IsTrue(diagnostics.GetHistory()[1].EndsWith("reason=e75_h100"));
        Assert.IsTrue(diagnostics.GetHistory()[2].EndsWith("reason=e75_h75"));
    }

    [TestMethod]
    [DataRow(100f, 100)]
    [DataRow(75.01f, 100)]
    [DataRow(75f, 75)]
    [DataRow(50f, 50)]
    [DataRow(25f, 25)]
    [DataRow(10f, 10)]
    [DataRow(0f, 0)]
    public void EnergyBandsRetainCanonicalThresholds(float value, int expected)
    {
        Assert.AreEqual(expected, ScannerRoomClientDiagnostics.EnergyBand(value));
    }

    [TestMethod]
    [DataRow(400f, 100)]
    [DataRow(300.01f, 100)]
    [DataRow(300f, 75)]
    [DataRow(200f, 50)]
    [DataRow(100f, 25)]
    [DataRow(40f, 10)]
    [DataRow(0f, 0)]
    public void HealthBandsAreNormalizedToCameraMaximum(float value, int expected)
    {
        Assert.AreEqual(expected, ScannerRoomClientDiagnostics.HealthBand(value));
    }
}
