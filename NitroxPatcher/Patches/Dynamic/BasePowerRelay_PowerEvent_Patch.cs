using System;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Suppresses only base-power voice callbacks during multiplayer initial reconciliation.
///     The underlying <see cref="PowerRelay"/> state and events remain untouched.
/// </summary>
public sealed class BasePowerRelay_PowerEvent_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    internal const float LoadSettleSeconds = 2f;

    private static readonly MethodInfo START = AccessTools.Method(typeof(BasePowerRelay), "Start");
    private static readonly MethodInfo POWER_DOWN_EVENT = AccessTools.Method(typeof(BasePowerRelay), "PowerDownEvent", [typeof(PowerRelay)]);
    private static readonly MethodInfo POWER_UP_EVENT = AccessTools.Method(typeof(BasePowerRelay), "PowerUpEvent", [typeof(PowerRelay)]);
    private static readonly ConditionalWeakTable<BasePowerRelay, LoadWindow> loadWindows = new();

    public static void StartPostfix(BasePowerRelay __instance)
    {
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        if (ShouldTrack(Multiplayer.Active, initialSyncCompleted))
        {
            loadWindows.Remove(__instance);
            loadWindows.Add(__instance, new LoadWindow(Time.time + LoadSettleSeconds));
        }
    }

    public static bool PowerDownPrefix(BasePowerRelay __instance) => Handle(__instance, "audio_down");

    public static bool PowerUpPrefix(BasePowerRelay __instance) => Handle(__instance, "audio_up");

    private static bool Handle(BasePowerRelay relay, string eventName)
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
        Resolve<BasePowerClientDiagnostics>().RecordAudioTransition(eventName, suppress, baseId,
            relay.GetPower(), relay.GetMaxPower(), initialSyncCompleted, waitScreenWaiting, suppressionReason);
        return !suppress;
    }

    internal static bool ShouldTrack(bool multiplayerActive, bool initialSyncCompleted) =>
        multiplayerActive && !initialSyncCompleted;

    internal static bool ShouldSuppress(bool tracked, bool initialSyncCompleted, float currentTime, float suppressUntil) =>
        tracked && (!initialSyncCompleted || currentTime < suppressUntil);

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, START, null, new Action<BasePowerRelay>(StartPostfix).Method);
        PatchMultiple(harmony, POWER_DOWN_EVENT, new Func<BasePowerRelay, bool>(PowerDownPrefix).Method);
        PatchMultiple(harmony, POWER_UP_EVENT, new Func<BasePowerRelay, bool>(PowerUpPrefix).Method);
    }

    private sealed record LoadWindow(float SuppressUntil);
}
