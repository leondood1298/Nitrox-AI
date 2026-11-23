using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Welder_OnHolster_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Welder t) => t.OnHolster());

    public static void Postfix()
    {
        if (Welder_Update_Patch.localWasWelding)
        {
            Welder_Update_Patch.localWasWelding = false;
            if (Player.main)
            {
                Resolve<LocalPlayer>().BroadcastWelderUse(welding: false);
            }
        }
    }
}
