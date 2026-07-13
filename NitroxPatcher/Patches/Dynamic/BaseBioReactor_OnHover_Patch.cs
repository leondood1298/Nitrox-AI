using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class BaseBioReactor_OnHover_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
	private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseBioReactor t) => t.OnHover());

	public static void Postfix(BaseBioReactor __instance)
	{
		string vanillaText = Language.main.GetFormat("UseBaseBioReactor", UnityEngine.Mathf.RoundToInt(__instance._powerSource.GetPower()), UnityEngine.Mathf.RoundToInt(__instance._powerSource.GetMaxPower()));
		HandReticle.main.SetText(HandReticle.TextType.Hand, $"{vanillaText}\n{ReactorFuelDisplay.GetStatus(__instance)}", false, GameInput.Button.LeftHand);
	}

	[GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
	public override void Patch(Harmony harmony)
	{
		PatchMultiple(harmony, TARGET_METHOD, null, new Action<BaseBioReactor>(Postfix).Method);
	}
}
