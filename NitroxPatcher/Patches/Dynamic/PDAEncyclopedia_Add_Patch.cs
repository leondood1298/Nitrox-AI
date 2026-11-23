using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class PDAEncyclopedia_Add_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => PDAEncyclopedia.Add(default, default, default));

    public static void Postfix(string key, PDAEncyclopedia.Entry entry, bool verbose, PDAEncyclopedia.EntryData __result)
    {
        // Single player saves nothing after the launch, so neither does the server for a game playing the ending
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted || LaunchRocket.launchStarted)
        {
            return;
        }

        // A found time capsule's entry (PDAEncyclopedia.AddTimeCapsule) travels with its content in TimeCapsuleCollected.
        // Its key is the capsule's id, which no other game has an entry for.
        if (entry != null && !string.IsNullOrEmpty(entry.timeCapsuleId))
        {
            return;
        }

        // Is null when it's a duplicate call
        if (__result != null)
        {
            Resolve<IPacketSender>().Send(new PDAEncyclopediaEntryAdd(key, verbose));
        }
    }
}
