using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The terminal takes the tablet at the click but opens the doors only when the player's cinematic ends. Its state is
///     stored then, together with the doors, so that a player who leaves during the cinematic leaves nothing behind.
/// </summary>
public sealed partial class PrecursorKeyTerminal_OnPlayerCinematicModeEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PrecursorKeyTerminal t) => t.OnPlayerCinematicModeEnd());

    public static void Postfix(PrecursorKeyTerminal __instance)
    {
        if (__instance.slotted && __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Entities>().BroadcastMetadataUpdate(id, new PrecursorKeyTerminalMetadata(__instance.slotted));
        }
    }
}
