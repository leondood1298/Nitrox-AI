using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     Bounded, single-line client evidence for base-power source reconciliation and relay voice transitions.
/// </summary>
public sealed class BasePowerClientDiagnostics
{
    internal const int SourceHistoryCapacity = 48;
    internal const int AudioHistoryCapacity = 16;
    internal const int HistoryCapacity = SourceHistoryCapacity + AudioHistoryCapacity + 2;

    private static int nextEpoch = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
    private readonly string epoch = unchecked((uint)Interlocked.Increment(ref nextEpoch)).ToString("x8", CultureInfo.InvariantCulture);
    private readonly List<string> history = new(HistoryCapacity);
    private readonly object sync = new();
    private int audioEntries;
    private int sourceEntries;
    private bool audioTruncated;
    private bool sourceTruncated;

    internal string Epoch => epoch;

    internal string? RecordSourceApply(NitroxId? sourceId, PowerSourceMetadata metadata, bool objectFound,
        bool initialSyncCompleted, bool waitScreenWaiting, string reason)
    {
        return Record(false, "source_apply", objectFound ? "ok" : "missing", null, sourceId, metadata.Power, metadata.MaxPower,
            initialSyncCompleted, waitScreenWaiting, $"{reason}_{metadata.SourceType}_{metadata.Revision}");
    }

    public string? RecordAudioTransition(string eventName, bool suppressed, NitroxId? baseId, float power, float maxPower,
        bool initialSyncCompleted, bool waitScreenWaiting, string? reason)
    {
        return Record(true, eventName, suppressed ? "suppress" : "pass", baseId, null, power, maxPower,
            initialSyncCompleted, waitScreenWaiting, reason ?? "live");
    }

    internal IReadOnlyList<string> GetHistory()
    {
        lock (sync)
        {
            return history.ToArray();
        }
    }

    private string? Record(bool audio, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason)
    {
        string line;
        lock (sync)
        {
            int capacity = audio ? AudioHistoryCapacity : SourceHistoryCapacity;
            bool atCapacity = audio ? audioEntries >= capacity : sourceEntries >= capacity;
            if (atCapacity)
            {
                bool alreadyTruncated = audio ? audioTruncated : sourceTruncated;
                if (alreadyTruncated)
                {
                    return null;
                }
                if (audio)
                {
                    audioTruncated = true;
                }
                else
                {
                    sourceTruncated = true;
                }
                line = FormatLine(history.Count + 1, audio ? "audio_trace_limit" : "source_trace_limit",
                    "truncated", null, null, 0f, 0f, initialSyncCompleted, waitScreenWaiting, $"capacity_{capacity}");
                history.Add(line);
            }
            else
            {
                if (audio)
                {
                    audioEntries++;
                }
                else
                {
                    sourceEntries++;
                }

                line = FormatLine(history.Count + 1, eventName, outcome, baseId, sourceId, power, maxPower,
                    initialSyncCompleted, waitScreenWaiting, reason);
                history.Add(line);
            }
        }
        Log.Info(line);
        return line;
    }

    private string FormatLine(int sequence, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason) =>
        FormattableString.Invariant(
            $"[BPD1] n={sequence} ep={epoch} side=C ev={Token(eventName)} out={Token(outcome)} base={ShortId(baseId)} source={ShortId(sourceId)} power={power:F2}/{maxPower:F2} initial={(initialSyncCompleted ? 1 : 0)} wait={(waitScreenWaiting ? 1 : 0)} reason={Token(reason)}");

    private static string ShortId(NitroxId? id)
    {
        string value = id?.ToString() ?? "-";
        return value.Length >= 23 && value[8] == '-' && value[13] == '-' && value[18] == '-'
            ? $"{value[..8]}.{value.Substring(19, 4)}"
            : value.Length > 8 ? value[..8] : value;
    }

    private static string Token(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        char[] buffer = new char[Math.Min(value.Length, 48)];
        int length = 0;
        foreach (char character in value)
        {
            char normalized = char.ToLowerInvariant(character);
            if (normalized is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.')
            {
                buffer[length++] = normalized;
            }
            else if (length > 0 && buffer[length - 1] != '_')
            {
                buffer[length++] = '_';
            }
            if (length == buffer.Length)
            {
                break;
            }
        }
        return new string(buffer, 0, length).Trim('_') is { Length: > 0 } token ? token : "-";
    }
}
