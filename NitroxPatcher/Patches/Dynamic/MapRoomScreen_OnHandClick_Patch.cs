using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A click on a Scanner Room screen puts the player into the view of a camera. The player needs the camera's
///     exclusive simulation lock for that, like for a vehicle they want to pilot (see
///     <see cref="PilotingChair_OnHandClick_Patch" />): the click is held back until the server grants the lock, and
///     then runs as the game wrote it.
/// </summary>
public sealed partial class MapRoomScreen_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomScreen t) => t.OnHandClick(default(GUIHand)));

    private static bool skipPrefix;

    public static bool Prefix(MapRoomScreen __instance, GUIHand guiHand)
    {
        return skipPrefix || Resolve<MapRoomCameras>().TryStartControl(__instance, 0, () =>
        {
            skipPrefix = true;
            try
            {
                __instance.OnHandClick(guiHand);
            }
            finally
            {
                skipPrefix = false;
            }
        });
    }
}
