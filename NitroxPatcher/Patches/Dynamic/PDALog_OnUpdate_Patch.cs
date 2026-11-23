using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     PDALog.OnUpdate marks the log entries timed between its last clock and the game time as new. The game time follows
///     the server's and a correction can move it back; the log's clock waits until the game time is past it again instead
///     of following it back and marking those entries once more.
/// </summary>
public sealed partial class PDALog_OnUpdate_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => PDALog.OnUpdate());

    public static bool Prefix() => PdaNotifications.MayAdvanceLogClock();
}
