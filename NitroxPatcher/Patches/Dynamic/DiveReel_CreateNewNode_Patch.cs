using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A node the player lays with a pathfinder tool is sent with the tool's trail (see <see cref="DiveReels" />).
/// </summary>
public sealed partial class DiveReel_CreateNewNode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DiveReel t) => t.CreateNewNode(default(Vector3), default(bool), default(bool)));

    public static void Postfix(DiveReel __instance, bool loadingNode)
    {
        // A node made while loading the trail (DiveReel.OnProtoDeserialize, DiveReels.ShowTrail) is already in it
        if (!loadingNode)
        {
            Resolve<DiveReels>().TrailChanged(__instance);
        }
    }
}
