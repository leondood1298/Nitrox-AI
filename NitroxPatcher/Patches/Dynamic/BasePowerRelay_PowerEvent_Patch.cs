using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Suppresses only base-power voice callbacks during multiplayer initial reconciliation.
///     The underlying <see cref="PowerRelay"/> state and events remain untouched.
/// </summary>
public sealed class BasePowerRelay_PowerEvent_Patch : NitroxPatch, IDynamicPatch
{
    internal const float LOAD_SETTLE_SECONDS = 2f;

    private static readonly MethodInfo TARGET_METHOD_START = Reflect.Method((BasePowerRelay t) => t.Start());
    private static readonly MethodInfo TARGET_METHOD_POWER_DOWN_EVENT = Reflect.Method((BasePowerRelay t) => t.PowerDownEvent(default));
    private static readonly MethodInfo TARGET_METHOD_POWER_UP_EVENT = Reflect.Method((BasePowerRelay t) => t.PowerUpEvent(default));
    private static readonly ConditionalWeakTable<BasePowerRelay, LoadWindow> loadWindows = new();

    public static void StartPostfix(BasePowerRelay __instance)
    {
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        if (ShouldTrack(Multiplayer.Active, initialSyncCompleted))
        {
            loadWindows.Remove(__instance);
            loadWindows.Add(__instance, new LoadWindow(Time.time + LOAD_SETTLE_SECONDS));
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

    public override void Patch(Harmony harmony)
    {
        PatchPostfix(harmony, TARGET_METHOD_START, ((Action<BasePowerRelay>)StartPostfix).Method);
        PatchPrefix(harmony, TARGET_METHOD_POWER_DOWN_EVENT, ((Func<BasePowerRelay, bool>)PowerDownPrefix).Method);
        PatchPrefix(harmony, TARGET_METHOD_POWER_UP_EVENT, ((Func<BasePowerRelay, bool>)PowerUpPrefix).Method);
    }

    private sealed record LoadWindow(float SuppressUntil);
}
