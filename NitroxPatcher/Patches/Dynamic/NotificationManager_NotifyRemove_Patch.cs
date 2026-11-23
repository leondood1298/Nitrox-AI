using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Runs whenever the PDA loses an unread marker: the player looked at it long enough (NotificationManager.LateUpdate,
///     UnregisterTarget) or the game removed it.
/// </summary>
public sealed partial class NotificationManager_NotifyRemove_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((NotificationManager t) => t.NotifyRemove(default));

    public static void Postfix(NotificationManager.NotificationId id)
    {
        Resolve<PdaNotifications>().Changed(id.group, id.key, false);
    }
}
