using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class TeleporterManager_ActivateTeleporter_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => TeleporterManager.ActivateTeleporter(default));

    public static void Prefix(string identifier)
    {
        Resolve<PrecursorTeleporters>().BroadcastActivation(identifier);
    }
}
