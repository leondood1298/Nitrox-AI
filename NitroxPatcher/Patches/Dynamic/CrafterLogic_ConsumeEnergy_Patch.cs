using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Marks the power cost of starting a craft as coming from a consumer that runs on one client only, so it is
///     reported to whoever simulates the power source. GhostCrafter.Craft is the only caller and runs for the crafting
///     player alone; other players replay the craft through CrafterLogic.Craft, which does not charge power.
/// </summary>
public sealed partial class CrafterLogic_ConsumeEnergy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => CrafterLogic.ConsumeEnergy(default, default));

    public static void Prefix()
    {
        Resolve<BasePowerConsumptionForwarder>().EnterConsumer();
    }

    public static void Finalizer()
    {
        Resolve<BasePowerConsumptionForwarder>().ExitConsumer();
    }
}
