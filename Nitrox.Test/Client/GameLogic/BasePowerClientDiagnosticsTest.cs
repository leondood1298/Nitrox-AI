using System.Globalization;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BasePowerClientDiagnosticsTest
{
    [TestMethod]
    public void ProducesCompactStableTransitionTrace()
    {
        BasePowerClientDiagnostics diagnostics = new();
        NitroxId baseId = new("11111111-1111-1111-1111-111111111111");

        string line = diagnostics.RecordAudioTransition("Audio Down", true, baseId, 0f, 2500f,
            initialSyncCompleted: false, waitScreenWaiting: true, "initial sync\r\n")!;

        Assert.AreEqual(
            $"[BPD1] n=1 ep={diagnostics.Epoch} side=C ev=audio_down out=suppress base=11111111.1111 source=- power=0.00/2500.00 initial=0 wait=1 reason=initial_sync",
            line);
        Assert.IsFalse(line.Contains('\r') || line.Contains('\n'));
    }

    [TestMethod]
    public void SourceTraceIncludesCanonicalTypeAndRevision()
    {
        BasePowerClientDiagnostics diagnostics = new();
        NitroxId sourceId = new("22222222-2222-2222-2222-222222222222");
        PowerSourceMetadata metadata = new(125.25f, 250f, BasePowerSourceType.THERMAL, 17);

        string line = diagnostics.RecordSourceApply(sourceId, metadata, true, false, true, "metadata")!;

        Assert.AreEqual(
            $"[BPD1] n=1 ep={diagnostics.Epoch} side=C ev=source_apply out=ok base=- source=22222222.2222 power=125.25/250.00 initial=0 wait=1 reason=metadata_thermal_17",
            line);
    }

    [TestMethod]
    public void SourceTraceRecordsMissingObjectExplicitly()
    {
        BasePowerClientDiagnostics diagnostics = new();
        NitroxId sourceId = new("33333333-3333-3333-3333-333333333333");
        PowerSourceMetadata metadata = new(0f, 75f, BasePowerSourceType.SOLAR, 4);

        string line = diagnostics.RecordSourceApply(sourceId, metadata, false, false, true, "packet")!;

        StringAssert.Contains(line, " ev=source_apply out=missing ");
        StringAssert.Contains(line, " source=33333333.3333 power=0.00/75.00 initial=0 wait=1 reason=packet_solar_4");
    }

    [TestMethod]
    public void AudioTraceStopsAtReservedCapacity()
    {
        BasePowerClientDiagnostics diagnostics = new();

        for (int i = 0; i < BasePowerClientDiagnostics.AudioHistoryCapacity + 10; i++)
        {
            diagnostics.RecordAudioTransition("audio_up", false, null, i, 100f, true, false, null);
        }

        Assert.AreEqual(BasePowerClientDiagnostics.AudioHistoryCapacity + 1, diagnostics.GetHistory().Count);
        StringAssert.Contains(diagnostics.GetHistory()[^1], " ev=audio_trace_limit out=truncated ");
        StringAssert.Contains(diagnostics.GetHistory()[^1], $" reason=capacity_{BasePowerClientDiagnostics.AudioHistoryCapacity}");
    }

    [TestMethod]
    public void SourceBudgetCannotConsumeReservedAudioEntries()
    {
        BasePowerClientDiagnostics diagnostics = new();
        PowerSourceMetadata metadata = new(10f, 75f, BasePowerSourceType.SOLAR, 1);

        for (int i = 0; i < BasePowerClientDiagnostics.SourceHistoryCapacity + 10; i++)
        {
            diagnostics.RecordSourceApply(new NitroxId(), metadata, true, false, true, "metadata");
        }
        string audio = diagnostics.RecordAudioTransition("audio_down", true, null, 0f, 75f, true, false,
            "load_settle")!;

        Assert.AreEqual(BasePowerClientDiagnostics.SourceHistoryCapacity + 2, diagnostics.GetHistory().Count);
        StringAssert.Contains(diagnostics.GetHistory()[^2], " ev=source_trace_limit out=truncated ");
        StringAssert.Contains(audio, " ev=audio_down out=suppress ");
    }

    [TestMethod]
    public void EpochIsStablePerInstanceAndDistinctAcrossInstances()
    {
        BasePowerClientDiagnostics first = new();
        BasePowerClientDiagnostics second = new();

        string firstDown = first.RecordAudioTransition("audio_down", true, null, 0f, 100f, false, true,
            "initial_sync")!;
        string firstUp = first.RecordAudioTransition("audio_up", true, null, 100f, 100f, true, false,
            "load_settle")!;
        string secondDown = second.RecordAudioTransition("audio_down", true, null, 0f, 100f, false, true,
            "initial_sync")!;

        Assert.AreNotEqual(first.Epoch, second.Epoch);
        StringAssert.Contains(firstDown, $" ep={first.Epoch} ");
        StringAssert.Contains(firstUp, $" ep={first.Epoch} ");
        StringAssert.Contains(secondDown, $" ep={second.Epoch} ");
    }

    [TestMethod]
    public void DistinguishesIdsThatShareFirstGuidGroup()
    {
        BasePowerClientDiagnostics diagnostics = new();
        NitroxId first = new("99e1d312-b017-474e-8c30-ffc66573465f");
        NitroxId second = new("99e1d312-b017-474e-72cf-00396573465f");

        string firstLine = diagnostics.RecordAudioTransition("audio_down", true, first, 0f, 100f, false, true,
            "initial_sync")!;
        string secondLine = diagnostics.RecordAudioTransition("audio_down", true, second, 0f, 100f, false, true,
            "initial_sync")!;

        StringAssert.Contains(firstLine, " base=99e1d312.8c30 ");
        StringAssert.Contains(secondLine, " base=99e1d312.72cf ");
    }

    [TestMethod]
    public void NumericFieldsUseInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            BasePowerClientDiagnostics diagnostics = new();

            string line = diagnostics.RecordAudioTransition("audio_up", false, null, 12.5f, 75.25f, true, false,
                "live")!;

            StringAssert.Contains(line, " power=12.50/75.25 ");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
