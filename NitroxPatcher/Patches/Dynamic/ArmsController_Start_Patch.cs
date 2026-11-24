using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class ArmsController_Start_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.Start());

    public static void Postfix(ArmsController __instance)
    {
        // Start takes the local player's PDA. Reconfigure skips the hands' targets while that PDA is open, so another
        // player's hands would miss the vehicle controls whenever the local player reads their PDA.
        if (RemoteArms.TryGetRemotePlayer(__instance, out RemotePlayer remotePlayer) && remotePlayer.Pda)
        {
            __instance.pda = remotePlayer.Pda;
        }
        __instance.Reconfigure(null);
    }
}
