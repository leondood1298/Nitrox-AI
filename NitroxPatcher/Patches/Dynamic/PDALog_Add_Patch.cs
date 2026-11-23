using System;
using System.Collections.Generic;
using System.Reflection;
using NitroxClient.Communication.Abstract;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class PDALog_Add_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => PDALog.Add(default, default));
    public static List<string> IgnoreKeys = new();

    public static void Postfix(string key, PDALog.EntryData __result)
    {
        // Single player saves nothing after the launch, so neither does the server for a game playing the ending
        if (IgnoreKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) > 0 || __result == null ||
            !PDALog.entries.TryGetValue(key, out PDALog.Entry entry) || LaunchRocket.launchStarted)
        {
            return;
        }
        Resolve<IPacketSender>().Send(new PDALogEntryAdd(key, entry.timestamp));
    }
}
