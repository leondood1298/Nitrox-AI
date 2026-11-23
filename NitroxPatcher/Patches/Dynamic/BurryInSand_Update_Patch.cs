using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Sand Shark bursts out of the sand, see <see cref="SandSharkBurrows" />.
/// </summary>
public sealed partial class BurryInSand_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BurryInSand t) => t.Update());

    /*
     * if (this.sandCoverFX != null && !this.sandCoverFX.isDiggingUp)
     * {
     *     this.PlaySandTrailFX();
     *     base.Invoke("StopSandTrailFX", 4f);
     *     this.sandCoverFX.DigUp();
     *     BurryInSand_Update_Patch.DugUp(this);   <--- [INSERTED LINE]
     * }
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchEndForward(new CodeMatch(OpCodes.Callvirt, Reflect.Method((VFXSandCover t) => t.DigUp())))
                                            .Advance(1)
                                            .Insert(new CodeInstruction(OpCodes.Ldarg_0),
                                                    new CodeInstruction(OpCodes.Call, Reflect.Method(() => DugUp(default))))
                                            .InstructionEnumeration();
    }

    public static void DugUp(BurryInSand burryInSand) => Resolve<SandSharkBurrows>().DugUp(burryInSand);
}
