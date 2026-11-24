using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Shows a shared picture in place of the game's loading of a local screenshot (see <see cref="PictureFrames" />).
/// </summary>
public sealed partial class PictureFrame_SetState_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PictureFrame t) => t.SetState(default));

    public static bool Prefix(PictureFrame __instance, PictureFrame.State newState)
    {
        return Resolve<PictureFrames>().SetState(__instance, newState);
    }
}
