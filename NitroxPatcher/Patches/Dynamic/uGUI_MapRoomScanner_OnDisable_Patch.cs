using System.Reflection;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game stops a room's scan when the room's screen is switched off: when the base loses power, and when the
///     room is destroyed. In multiplayer that would stop it for everyone, because of something that happened in one
///     player's game and may not even have happened in the others'. So the game's method never runs: a scan goes on
///     until a player stops it, and merely waits while there is no power.
/// </summary>
public sealed partial class uGUI_MapRoomScanner_OnDisable_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.OnDisable());

    public static bool Prefix() => false;
}
