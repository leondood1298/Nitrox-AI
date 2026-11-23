using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The terminal takes the power crystal at the click but opens the teleporter only when the player's cinematic ends.
///     The crystal's removal and the terminal's state are both sent then, so that a player who leaves during the cinematic
///     leaves nothing behind.
/// </summary>
public sealed partial class PrecursorTeleporterActivationTerminal_OnPlayerCinematicModeEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorTeleporterActivationTerminal t) => t.OnPlayerCinematicModeEnd(default(PlayerCinematicController)));

    public static void Prefix(PrecursorTeleporterActivationTerminal __instance)
    {
        if (__instance.crystalObject.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(id));
        }
    }

    public static void Postfix(PrecursorTeleporterActivationTerminal __instance)
    {
        if (__instance.unlocked && __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Entities>().BroadcastMetadataUpdate(id, new PrecursorTeleporterActivationTerminalMetadata(__instance.unlocked));
        }
    }
}
