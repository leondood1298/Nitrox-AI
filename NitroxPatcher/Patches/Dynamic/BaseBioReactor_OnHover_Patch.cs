using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseBioReactor_OnHover_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseBioReactor t) => t.OnHover());

    public static void Postfix(BaseBioReactor __instance)
    {
        string vanillaText = Language.main.GetFormat("UseBaseBioReactor", UnityEngine.Mathf.RoundToInt(__instance._powerSource.GetPower()), UnityEngine.Mathf.RoundToInt(__instance._powerSource.GetMaxPower()));
        HandReticle.main.SetText(HandReticle.TextType.Hand, $"{vanillaText}\n{ReactorFuelDisplay.GetStatus(__instance)}", false, GameInput.Button.LeftHand);
    }
}
