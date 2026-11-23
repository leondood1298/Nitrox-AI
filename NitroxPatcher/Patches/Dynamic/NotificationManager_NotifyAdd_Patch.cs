using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Runs whenever the PDA gets an unread marker it did not have.
/// </summary>
public sealed partial class NotificationManager_NotifyAdd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((NotificationManager t) => t.NotifyAdd(default, default));

    public static void Postfix(NotificationManager.NotificationId id)
    {
        Resolve<PdaNotifications>().Changed(id.group, id.key, true);
    }
}
