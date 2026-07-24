using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCamera_CaptureScreenAndExitLockedMode_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private delegate IEnumerator PostfixDelegate(IEnumerator result, MapRoomCamera instance);

    private static readonly MethodInfo TARGET_METHOD = typeof(MapRoomCamera).GetMethod(
        "CaptureScreenAndExitLockedMode", BindingFlags.Instance | BindingFlags.NonPublic);

    public static IEnumerator Postfix(IEnumerator __result, MapRoomCamera __instance) =>
        PublishAfterCapturedFrame(__result, () => Resolve<MapRoomCameras>().BroadcastPreview(__instance));

    internal static IEnumerator PublishAfterCapturedFrame(IEnumerator routine, Action publish)
    {
        bool published = false;
        while (routine.MoveNext())
        {
            object yielded = routine.Current;
            yield return yielded;
            if (!published && yielded is WaitForEndOfFrame)
            {
                published = true;
                try
                {
                    publish();
                }
                catch (Exception ex)
                {
                    // A cosmetic preview failure must not prevent the wrapped vanilla coroutine from
                    // removing its command buffer and returning the player from locked camera mode.
                    Log.Error(ex, "Failed to publish Scanner Room camera preview");
                }
            }
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, null, new PostfixDelegate(Postfix).Method);
    }
}
