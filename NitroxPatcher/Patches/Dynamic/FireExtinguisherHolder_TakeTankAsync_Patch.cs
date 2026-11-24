using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using NitroxPatcher.Helper;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts the FireExtinguisherHolder's metadata when picked up.
/// </summary>
public sealed partial class FireExtinguisherHolder_TakeTankAsync_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = AccessTools.EnumeratorMoveNext(Reflect.Method((FireExtinguisherHolder t) => t.TakeTankAsync()));

    public static readonly OpCode INJECTION_OPCODE = OpCodes.Call;
    public static readonly object INJECTION_OPERAND = Reflect.Method(() => CraftData.AddToInventoryAsync(default(TechType), default(IOut<GameObject>), default(int), default(bool), default(bool)));

    public static readonly OpCode FUEL_INJECTION_OPCODE = OpCodes.Stfld;
    public static readonly object FUEL_INJECTION_OPERAND = Reflect.Field((FireExtinguisher t) => t.fuel);

    public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;

            if (instruction.opcode.Equals(INJECTION_OPCODE) && instruction.operand.Equals(INJECTION_OPERAND))
            {
                /*
                 * Injects: Callback(this);
                 */
                yield return original.Ldloc<FireExtinguisherHolder>();
                yield return new CodeInstruction(OpCodes.Call, ((Action<FireExtinguisherHolder>)Callback).Method);
            }
            else if (instruction.opcode.Equals(FUEL_INJECTION_OPCODE) && instruction.operand.Equals(FUEL_INJECTION_OPERAND))
            {
                /*
                 * After: gameObject.GetComponent<FireExtinguisher>().fuel = this.fuel;
                 * Injects: FuelSetCallback(gameObject);
                 */
                yield return original.Ldloc<GameObject>();
                yield return new CodeInstruction(OpCodes.Call, ((Action<GameObject>)FuelSetCallback).Method);
            }
        }
    }

    /// <summary>
    ///     The taken extinguisher gets the holder's fuel only after it was picked up, so its fuel is reported again.
    /// </summary>
    public static void FuelSetCallback(GameObject tank)
    {
        Resolve<FireExtinguishers>().FuelTakenFromHolder(tank);
    }

    public static void Callback(FireExtinguisherHolder holder)
    {
        // We force this state earlier because it'll be read by the metadata extractor
        holder.hasTank = false;

        if (holder.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Entities>().EntityMetadataChanged(holder, id);
        }
    }
}
