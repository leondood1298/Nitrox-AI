using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Nitrox.Model.GameLogic.PlayerAnimation;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Player_OnKill_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Player t) => t.OnKill(default(DamageType)));

    private static readonly MethodInfo SKIP_METHOD = Reflect.Method(() => GameModeUtils.IsPermadeath());

    /// <summary>
    ///     Shows other players the same death animation as the one <see cref="Player.OnKill" /> picked for this player.
    /// </summary>
    public static void Postfix(DamageType damageType)
    {
        AnimChangeType animation = damageType switch
        {
            DamageType.Fire => AnimChangeType.DEATH_FIRE,
            DamageType.Explosive => AnimChangeType.DEATH_EXPLOSION,
            _ => AnimChangeType.DEATH
        };
        Resolve<LocalPlayer>().AnimationChange(animation, AnimChangeState.ON);
    }

    public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> instructionList = instructions.ToList();
        /**
        * Skips
        * if (GameModeUtils.IsPermadeath())
        * {
        *      SaveLoadManager.main.ClearSlotAsync(SaveLoadManager.main.GetCurrentSlot());
        *      this.EndGame();
        *      return;
        * }
        */
        for (int i = 0; i < instructionList.Count; i++)
        {
            CodeInstruction instr = instructionList[i];

            if (instr.opcode == OpCodes.Call && instr.operand.Equals(SKIP_METHOD))
            {
                CodeInstruction newInstr = new(OpCodes.Ldc_I4_0);
                newInstr.labels = instr.labels;
                yield return newInstr;
            }
            else
            {
                yield return instr;
            }
        }
    }
}
