using System.Reflection;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game stops a room's scan when the room's screen is switched off. The room switches it off when it loses
///     power, but also when it starts without power, and the screen goes with a room that is destroyed; and all of
///     that happens in every player's game. In multiplayer each of these would stop the scan for everyone. So the
///     game's method never runs. A real loss of power still stops the scan: <see cref="NitroxClient.GameLogic.MapRoomScans" />
///     watches the room's power in the game of the player who simulates the room, not its screen.
/// </summary>
public sealed partial class uGUI_MapRoomScanner_OnDisable_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.OnDisable());

    public static bool Prefix() => false;
}
