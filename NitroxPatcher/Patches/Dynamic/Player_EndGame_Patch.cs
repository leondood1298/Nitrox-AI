using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     In hardcore, <see cref="Player.ResetPlayerOnDeath" /> ends the game with <see cref="Player.EndGame" /> and returns
///     before it raises <see cref="Player.playerDeathEvent" />, so the death never reached the server or the others
///     (<see cref="NitroxClient.MonoBehaviours.PlayerDeathBroadcaster" /> listens to that event). It is reported here.
/// </summary>
public sealed partial class Player_EndGame_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Player t) => t.EndGame());

    public static void Prefix(Player __instance)
    {
        Resolve<LocalPlayer>().BroadcastDeath(__instance.transform.position);
    }
}
