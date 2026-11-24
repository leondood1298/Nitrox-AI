using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Tells the others a vending machine was used, so that its interval runs in every game.
/// </summary>
public sealed partial class VendingMachine_OnUse_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((VendingMachine t) => t.OnUse(default(HandTargetEventData)));

    public static void Prefix(VendingMachine __instance, out bool __state)
    {
        __state = __instance.GetCanBeUsed();
    }

    public static void Postfix(VendingMachine __instance, bool __state)
    {
        if (__state && __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<IPacketSender>().Send(new VendingMachineUse(id));
        }
    }
}
