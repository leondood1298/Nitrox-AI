using System.Runtime.CompilerServices;
using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Suppresses only base-power voice callbacks during multiplayer initial reconciliation.
///     The underlying <see cref="PowerRelay"/> state and events remain untouched.
/// </summary>
public sealed class BasePowerRelayAudioSuppressor(BasePowerClientDiagnostics diagnostics)
{
    /// <summary>
    ///     How long after the initial sync a relay that was created during it stays silent. A guess, not measured.
    /// </summary>
    internal const float LOAD_SETTLE_SECONDS = 2f;

    private readonly ConditionalWeakTable<BasePowerRelay, LoadWindow> loadWindows = new();

    /// <summary>
    ///     Call when a relay starts. A relay created before initial sync has completed is remembered so that its
    ///     voice callbacks stay silent until the restored power state has settled.
    /// </summary>
    public void TrackIfLoading(BasePowerRelay relay)
    {
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        if (ShouldTrack(Multiplayer.Active, initialSyncCompleted))
        {
            loadWindows.Remove(relay);
            loadWindows.Add(relay, new LoadWindow(Time.time + LOAD_SETTLE_SECONDS));
        }
    }

    /// <returns>False when the voice callback <paramref name="eventName" /> must be skipped for this relay.</returns>
    public bool ShouldPlay(BasePowerRelay relay, string eventName)
    {
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        bool waitScreenWaiting = WaitScreen.IsWaiting;
        bool tracked = loadWindows.TryGetValue(relay, out LoadWindow? window);
        float suppressUntil = window?.SuppressUntil ?? float.NegativeInfinity;
        bool suppress = ShouldSuppress(tracked, initialSyncCompleted, Time.time, suppressUntil);
        string suppressionReason = !tracked ? "live" : !initialSyncCompleted ? "initial_sync" : suppress ? "load_settle" : "live";
        if (tracked && !suppress)
        {
            loadWindows.Remove(relay);
        }

        NitroxId? baseId = null;
        if (relay.subRoot && relay.subRoot.TryGetNitroxId(out NitroxId id))
        {
            baseId = id;
        }
        diagnostics.RecordAudioTransition(eventName, suppress, baseId,
            relay.GetPower(), relay.GetMaxPower(), initialSyncCompleted, waitScreenWaiting, suppressionReason);
        return !suppress;
    }

    internal static bool ShouldTrack(bool multiplayerActive, bool initialSyncCompleted) =>
        multiplayerActive && !initialSyncCompleted;

    internal static bool ShouldSuppress(bool tracked, bool initialSyncCompleted, float currentTime, float suppressUntil) =>
        tracked && (!initialSyncCompleted || currentTime < suppressUntil);

    private sealed record LoadWindow(float SuppressUntil);
}
