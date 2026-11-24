using System.IO;
using System.Reflection;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Persistent;

/// <summary>
///     Changes Subnautica screenshot save location to <see cref="NitroxUser.ScreenshotsPath" />.
/// </summary>
public partial class ScreenshotManager_Combine_Patch : NitroxPatch, IPersistentPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => ScreenshotManager.Combine(default(string), default(string)));

    public static bool Prefix(string path1, string path2, ref string? __result)
    {
        // A single-player game keeps its screenshots in its save, where its picture frames and gallery find them.
        if (!Multiplayer.Active)
        {
            return true;
        }
        __result = path2 switch
        {
            ScreenshotManager.screenshotsFolderName => NitroxDirectory.ScreenshotsPath,
            { } filename when path1 == ScreenshotManager.savePath => Path.Combine(NitroxDirectory.ScreenshotsPath, Path.GetFileName(filename)),
            _ => null
        };
        return __result == null; // Run original method if we didn't handle (result is null).
    }
}
