using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCamera_ExitLockedMode_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
	private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.ExitLockedMode(resetPlayerPosition: false));

	public static void Postfix(MapRoomCamera __instance)
	{
		if (!PacketSuppressor<MapRoomCameraControl>.IsSuppressed)
		{
			NitroxPatch.Resolve<MapRoomCameras>().BroadcastControl(__instance, isControlling: false);
		}
	}

	[GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
	public override void Patch(Harmony harmony)
	{
		PatchMultiple(harmony, TARGET_METHOD, null, new Action<MapRoomCamera>(Postfix).Method);
	}
}


