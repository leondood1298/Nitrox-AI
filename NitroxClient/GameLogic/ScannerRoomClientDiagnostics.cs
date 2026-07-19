using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace NitroxClient.GameLogic;

/// <summary>
///     Compact client-side evidence for Scanner Room requests and canonical applies.
///     It intentionally omits movement and unchanged component packets.
/// </summary>
public sealed class ScannerRoomClientDiagnostics
{
    internal const int HistoryCapacity = 128;
    private static int nextEpoch = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
    private readonly Queue<string> history = new(HistoryCapacity);
    private readonly Dictionary<NitroxId, (int Energy, int Health)> componentBands = [];
    private readonly object sync = new();
    private readonly string epoch = unchecked((uint)Interlocked.Increment(ref nextEpoch)).ToString("x8", CultureInfo.InvariantCulture);
    private long nextSequence;

    internal string Epoch => epoch;

    internal string Record(string eventName, string outcome, NitroxId? cameraId = null, NitroxId? roomId = null,
        long? revision = null, int? slot = null, string? reason = null)
    {
        string line;
        lock (sync)
        {
            long sequence = ++nextSequence;
            line = FormattableString.Invariant(
                $"[SRD1] n={sequence} ep={epoch} side=C ev={Token(eventName)} out={Token(outcome)} room={ShortId(roomId)} cam={ShortId(cameraId, distinguishSiblingCamera: true)} slot={Format(slot)} rev={Format(revision)} reason={Token(reason)}");
            if (history.Count == HistoryCapacity)
            {
                history.Dequeue();
            }
            history.Enqueue(line);
            if (outcome is "reject" or "diverge")
            {
                Log.Warn(line);
            }
            else
            {
                Log.Info(line);
            }
        }
        return line;
    }

    internal void RecordComponentApplied(NitroxId cameraId, float energy, float health, long revision, bool objectFound)
    {
        int energyBand = EnergyBand(energy);
        int healthBand = HealthBand(health);
        bool shouldRecord;
        lock (sync)
        {
            shouldRecord = !componentBands.TryGetValue(cameraId, out (int Energy, int Health) previous) ||
                           previous.Energy != energyBand || previous.Health != healthBand;
            componentBands[cameraId] = (energyBand, healthBand);
        }
        if (shouldRecord)
        {
            Record("component_apply", objectFound ? "ok" : "diverge", cameraId, revision: revision,
                reason: objectFound ? $"e{energyBand}_h{healthBand}" : "missing_object");
        }
    }

    internal IReadOnlyList<string> GetHistory()
    {
        lock (sync)
        {
            return history.ToArray();
        }
    }

    internal static int EnergyBand(float energy) => ComponentBand(energy, MapRoomCameraRecord.MAX_ENERGY);

    internal static int HealthBand(float health) => ComponentBand(health, MapRoomCameraRecord.MAX_HEALTH);

    private static int ComponentBand(float value, float maximum) => value switch
    {
        <= 0f => 0,
        _ when value <= maximum * 0.1f => 10,
        _ when value <= maximum * 0.25f => 25,
        _ when value <= maximum * 0.5f => 50,
        _ when value <= maximum * 0.75f => 75,
        _ => 100
    };

    private static string ShortId(NitroxId? id, bool distinguishSiblingCamera = false)
    {
        string value = id?.ToString() ?? "-";
        // Scanner Room sibling camera IDs intentionally share their first GUID
        // group. Keep the log token compact while retaining the distinguishing
        // portion of the fourth group.
        return distinguishSiblingCamera && value.Length >= 23 && value[8] == '-' && value[13] == '-' && value[18] == '-'
            ? $"{value[..8]}.{value.Substring(19, 4)}"
            : value.Length > 8 ? value[..8] : value;
    }

    private static string Token(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }
        StringBuilder result = new(Math.Min(value.Length, 48));
        foreach (char character in value)
        {
            char normalized = char.ToLowerInvariant(character);
            if (IsAsciiLetterOrDigit(normalized) || normalized is '-' or '_' or '.')
            {
                result.Append(normalized);
            }
            else if (result.Length > 0 && result[^1] != '_')
            {
                result.Append('_');
            }
            if (result.Length == 48)
            {
                break;
            }
        }
        return result.ToString().Trim('_') is { Length: > 0 } token ? token : "-";
    }

    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    private static string Format(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';
}
