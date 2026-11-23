using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game asks its online service with the player's own account whether a time capsule lies at a spot in the world
///     and what it holds, so every player can get another answer. The answer this game gets is offered to the server, which
///     keeps the first one for everybody (<see cref="TimeCapsules" />).
/// </summary>
public sealed partial class PlayerTimeCapsule_SpawnRoutine_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = AccessTools.EnumeratorMoveNext(Reflect.Method((PlayerTimeCapsule t) => t.SpawnRoutine()));

    /*
     * client.Spawn(instanceId, id);
     * is replaced by
     * PlayerTimeCapsule_SpawnRoutine_Patch.Spawn(client, instanceId, id);
     *
     * client.DoNotSpawn();
     * is replaced by
     * PlayerTimeCapsule_SpawnRoutine_Patch.DoNotSpawn(client, this.success);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Whether the service answered at all; it is false after the game gave up reaching it
        FieldInfo successField = AccessTools.Field(TARGET_METHOD.DeclaringType, "<success>5__5");

        return new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Callvirt, Reflect.Method((TimeCapsule t) => t.Spawn(default, default))))
                                            .ThrowIfInvalid("Could not find the call to TimeCapsule.Spawn")
                                            .SetAndAdvance(OpCodes.Call, Reflect.Method(() => Spawn(default, default, default)))
                                            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, Reflect.Method((TimeCapsule t) => t.DoNotSpawn())))
                                            .ThrowIfInvalid("Could not find the call to TimeCapsule.DoNotSpawn")
                                            .SetAndAdvance(OpCodes.Ldarg_0, null)
                                            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, successField),
                                                              new CodeInstruction(OpCodes.Call, Reflect.Method(() => DoNotSpawn(default, default))))
                                            .InstructionEnumeration();
    }

    public static void Spawn(TimeCapsule capsule, string instanceId, string contentId)
    {
        Resolve<TimeCapsules>().OnServiceSpawned(capsule, instanceId, contentId);
    }

    public static void DoNotSpawn(TimeCapsule capsule, bool serviceAnswered)
    {
        Resolve<TimeCapsules>().OnServiceDidNotSpawn(capsule, serviceAnswered);
    }
}
