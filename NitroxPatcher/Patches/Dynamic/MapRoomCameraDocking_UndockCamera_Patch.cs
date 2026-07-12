using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCameraDocking_UndockCamera_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
	private delegate void PrefixDelegate(MapRoomCameraDocking instance, out MapRoomCamera state);
	private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCameraDocking t) => t.UndockCamera());

	public static void Prefix(MapRoomCameraDocking __instance, out MapRoomCamera __state)
	{
		__state = __instance.camera;
	}

	public static void Postfix(MapRoomCameraDocking __instance, MapRoomCamera __state)
	{
		NitroxPatch.Resolve<MapRoomCameras>().BroadcastUndock(__instance, __state);
	}

	[GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
	public override void Patch(Harmony harmony)
	{
		PatchMultiple(harmony, TARGET_METHOD, new PrefixDelegate(Prefix).Method, new Action<MapRoomCameraDocking, MapRoomCamera>(Postfix).Method);
	}
}
