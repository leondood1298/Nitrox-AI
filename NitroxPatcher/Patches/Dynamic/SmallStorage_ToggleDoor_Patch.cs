using System.Reflection;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Shows the lid of a waterproof locker opening and closing to the other players (the game toggles it when the locker
///     is opened and when its PDA closes).
/// </summary>
public sealed partial class SmallStorage_ToggleDoor_Patch : NitroxPatch, IDynamicPatch
{
    public const string OPENED_PARAMETER = "opened";

    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SmallStorage t) => t.ToggleDoor());

    public static void Postfix(SmallStorage __instance)
    {
        if (__instance.animator && __instance.TryGetComponentInParent(out NitroxEntity entity, true))
        {
            Resolve<IPacketSender>().Send(new SmallStorageLidChanged(entity.Id, __instance.animator.GetBool(OPENED_PARAMETER)));
        }
    }
}
