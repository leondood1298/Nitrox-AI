using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCamera_ControlCamera_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
	private delegate bool PrefixDelegate(MapRoomCamera instance, out bool state);
	private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.ControlCamera(null));

	public static bool Prefix(MapRoomCamera __instance, out bool __state)
	{
		__state = PacketSuppressor<MapRoomCameraControl>.IsSuppressed ||
			NitroxPatch.Resolve<MapRoomCameras>().CanBeginControl(__instance);
		return __state;
	}

	public static void Postfix(MapRoomCamera __instance, bool __state)
	{
		if (__state && !PacketSuppressor<MapRoomCameraControl>.IsSuppressed)
		{
			NitroxPatch.Resolve<MapRoomCameras>().BroadcastControl(__instance, isControlling: true);
		}
	}

	[GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
	public override void Patch(Harmony harmony)
	{
		PatchMultiple(harmony, TARGET_METHOD, new PrefixDelegate(Prefix).Method,
			new Action<MapRoomCamera, bool>(Postfix).Method);
	}
}

