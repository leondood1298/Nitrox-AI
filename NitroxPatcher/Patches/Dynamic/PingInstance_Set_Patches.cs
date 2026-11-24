using System;
using System.Reflection;
using HarmonyLib;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.InitialSync;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Notice the server of a Ping color change
/// </summary>
public sealed class PingInstance_Set_Patches : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD_COLOR = Reflect.Method((PingInstance t) => t.SetColor(default));
    private static readonly MethodInfo TARGET_METHOD_VISIBLE = Reflect.Method((PingInstance t) => t.SetVisible(default));

    public static void PrefixColor(PingInstance __instance, int index)
    {
        // Only notice server if there's a change on client-side
        if (__instance.colorIndex == index)
        {
            return;
        }

        if (PlayerPreferencesInitialSyncProcessor.TryGetKeyForPingInstance(__instance, out string pingKey, out bool _))
        {
            Send(pingKey, __instance.visible, index);
        }
    }

    public static void PrefixVisible(PingInstance __instance, bool value)
    {
        // Only notice server if there's a change on client-side
        if (__instance.visible == value)
        {
            return;
        }

        if (PlayerPreferencesInitialSyncProcessor.TryGetKeyForPingInstance(__instance, out string pingKey, out bool _))
        {
            Send(pingKey, value, __instance.colorIndex);
        }
    }

    private static void Send(string pingKey, bool visible, int color)
    {
        Resolve<IPacketSender>().Send(new SignalPingPreferenceChanged(pingKey, visible, color));
        // A ping that registers again later (a beacon thrown, moved by another player) is given this, not the join's value
        Resolve<PingPreferences>().Remember(pingKey, visible, color);
    }

    public override void Patch(Harmony harmony)
    {
        PatchPrefix(harmony, TARGET_METHOD_COLOR, ((Action<PingInstance, int>)PrefixColor).Method);
        PatchPrefix(harmony, TARGET_METHOD_VISIBLE, ((Action<PingInstance, bool>)PrefixVisible).Method);
    }
}
