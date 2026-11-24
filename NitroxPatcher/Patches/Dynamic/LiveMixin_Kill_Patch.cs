using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class LiveMixin_Kill_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((LiveMixin t) => t.Kill(default));

    public static void Prefix()
    {
        Resolve<LiveMixinManager>().EnterKill();
    }

    public static void Finalizer()
    {
        Resolve<LiveMixinManager>().ExitKill();
    }

    public static void Postfix(LiveMixin __instance)
    {
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }

        // A camera's health of zero has to reach the other players before its removal below: from it their games
        // know that it was destroyed and show it (see MapRoomCameraStates.ShowDestructionIfKilled).
        if (__instance.TryGetComponent(out MapRoomCamera camera))
        {
            Resolve<MapRoomCameraStates>().ReportIfChanged(camera);
        }

        // We don't broadcast if we don't have objectId or if the object is whitelisted,
        // in which case kill broadcast is managed differently
        if (!__instance.TryGetNitroxId(out NitroxId objectId) ||
            Resolve<LiveMixinManager>().IsWhitelistedUpdateType(__instance))
        {
            return;
        }
        
        // Some objects don't have destroyOnDeath but we still need to broadcast the death
        // (because the destruction is managed by another script)
        if (__instance.destroyOnDeath || Resolve<LiveMixinManager>().ShouldBroadcastDeath(__instance) || LiveMixinManager.IsHarvestedToDeath(__instance))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(objectId));
        }
    }
}
