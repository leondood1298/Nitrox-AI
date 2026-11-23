using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Shares the screenshot a player puts into a picture frame (see <see cref="PictureFrames" />).
/// </summary>
public sealed partial class PictureFrame_SelectImage_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PictureFrame t) => t.SelectImage(default));

    public static void Prefix(PictureFrame __instance, out string __state)
    {
        __state = __instance.fileName;
    }

    public static void Postfix(PictureFrame __instance, string __state)
    {
        Resolve<PictureFrames>().OnPictureSelected(__instance, __state);
    }
}
