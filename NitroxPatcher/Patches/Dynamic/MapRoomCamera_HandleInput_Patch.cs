using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCamera_HandleInput_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
	private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.HandleInput());

	public static void Postfix(MapRoomCamera __instance)
	{
		MapRoomCameras cameras = NitroxPatch.Resolve<MapRoomCameras>();
		cameras.BroadcastLightIfChanged(__instance);
		cameras.BroadcastComponentStateIfChanged(__instance);
	}

	[GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
	public override void Patch(Harmony harmony)
	{
		PatchMultiple(harmony, TARGET_METHOD, null, new Action<MapRoomCamera>(Postfix).Method);
	}
}


