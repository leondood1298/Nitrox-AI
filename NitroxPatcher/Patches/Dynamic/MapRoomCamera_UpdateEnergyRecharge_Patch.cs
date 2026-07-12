using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Ensures docked camera charging and its resulting component broadcast occur only on the Scanner Room's simulation owner.
/// </summary>
public sealed class MapRoomCamera_UpdateEnergyRecharge_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
	private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.UpdateEnergyRecharge());

	public static bool Prefix(MapRoomCamera __instance)
	{
		NitroxPatch.Resolve<MapRoomCameras>().UpdateEnergyRecharge(__instance);
		return false;
	}

	[GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
	public override void Patch(Harmony harmony)
	{
		PatchMultiple(harmony, TARGET_METHOD, new Func<MapRoomCamera, bool>(Prefix).Method);
	}
}
