using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

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
        Assert.AreEqual($"[SRD1] n=1 ep={diagnostics.Epoch} side=C ev=dock_apply out=ok room=- cam=11111111 slot=1 rev=4 reason=state_applied", first);
        Assert.AreEqual(ScannerRoomClientDiagnostics.HistoryCapacity, diagnostics.GetHistory().Count);
        Assert.IsTrue(diagnostics.GetHistory()[0].Contains(" n=24 "));
        Assert.IsFalse(diagnostics.GetHistory().Any(line => line.Contains('\r') || line.Contains('\n')));
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

        diagnostics.RecordComponentApplied(cameraId, 100f, 100f, 1, true);
        diagnostics.RecordComponentApplied(cameraId, 99f, 98f, 2, true);
        diagnostics.RecordComponentApplied(cameraId, 75f, 98f, 3, true);
        diagnostics.RecordComponentApplied(cameraId, 74f, 74f, 4, true);

        Assert.AreEqual(3, diagnostics.GetHistory().Count);
        Assert.IsTrue(diagnostics.GetHistory()[0].EndsWith("reason=e100_h100"));
        Assert.IsTrue(diagnostics.GetHistory()[1].EndsWith("reason=e75_h100"));
        Assert.IsTrue(diagnostics.GetHistory()[2].EndsWith("reason=e75_h75"));
    }
}
