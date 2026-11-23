using System;
using System.Collections;
using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCamera_CaptureScreenAndExitLockedMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.CaptureScreenAndExitLockedMode());

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
}
