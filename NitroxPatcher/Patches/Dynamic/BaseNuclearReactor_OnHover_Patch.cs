using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseNuclearReactor_OnHover_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.OnHover());

    public static void Postfix(BaseNuclearReactor __instance)
    {
        string vanillaText = Language.main.GetFormat("UseBaseNuclearReactor", UnityEngine.Mathf.RoundToInt(__instance._powerSource.GetPower()), UnityEngine.Mathf.RoundToInt(__instance._powerSource.GetMaxPower()));
        HandReticle.main.SetText(HandReticle.TextType.Hand, $"{vanillaText}\n{ReactorFuelDisplay.GetStatus(__instance)}", false, GameInput.Button.LeftHand);
    }
}
