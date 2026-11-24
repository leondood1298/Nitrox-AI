using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Syncs fish deletion once it ages to adult state.
/// </summary>
public sealed partial class WaterParkCreature_ManagedUpdate_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((WaterParkCreature t) => t.ManagedUpdate());

    /*
     * MODIFIED:
     * WaterParkCreature.Born(adultPrefab, this.currentWaterPark, base.transform.position);
     * WaterParkCreature_ManagedUpdate_Patch.CreatureDestroyedCallback(this); <--- INSERTED
     * base.SetWaterPark(null);
     * UnityEngine.Object.Destroy(base.gameObject);
     *
     * The callback has to come before SetWaterPark(null): it needs the containment to know who decides the growth,
     * and SetWaterPark(null) forgets it.
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new CodeMatcher(instructions).MatchStartForward([
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldnull),
            new CodeMatch(OpCodes.Call, Reflect.Method((WaterParkItem t) => t.SetWaterPark(default))),
        ]);
        List<Label> labels = matcher.Instruction.ExtractLabels();
        return matcher.Insert([
                          new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
                          new CodeInstruction(OpCodes.Call, Reflect.Method(() => CreatureDestroyedCallback(default)))
                      ])
                      .InstructionEnumeration();
    }

    public static void CreatureDestroyedCallback(WaterParkCreature waterParkCreature)
    {
        // We don't manage eggs with no id here
        if (!waterParkCreature.TryGetNitroxId(out NitroxId creatureId))
        {
            return;
        }

        // It is VERY IMPORTANT to check for simulation ownership on the water park and not on the egg
        // since that's the convention we chose
        if (waterParkCreature.currentWaterPark.TryGetNitroxId(out NitroxId waterParkId) &&
            Resolve<SimulationOwnership>().HasAnyLockType(waterParkId))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(creatureId));
        }
    }
}
