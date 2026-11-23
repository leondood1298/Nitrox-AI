using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the game of the player simulating a void Ghost Leviathan hunts with it and removes it (<see cref="SharedCreatureSpawns.MayUpdateVoidGhost" />).
/// </summary>
public sealed partial class GhostLeviatanVoid_UpdateVoidBehaviour_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GhostLeviatanVoid t) => t.UpdateVoidBehaviour());

    public static bool Prefix(GhostLeviatanVoid __instance) => Resolve<SharedCreatureSpawns>().MayUpdateVoidGhost(__instance);
}
