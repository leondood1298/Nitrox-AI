using System.Reflection.Emit;
using HarmonyLib;
using NitroxTest.Patcher;

namespace NitroxPatcher.Patches.Persistent;

[TestClass]
public class MainGameController_StartGame_PatchTest
{
    [TestMethod]
    public void TheGamesStartGameGetsTheLoadingCompletedCall()
    {
        List<CodeInstruction> original = PatchTestHelper.GetInstructionsFromMethod(MainGameController_StartGame_Patch.TARGET_METHOD).ToList();

        List<CodeInstruction> result = MainGameController_StartGame_Patch.Transpiler(MainGameController_StartGame_Patch.TARGET_METHOD, original).ToList();

        result.Should().HaveCount(original.Count + 1);
    }

    [TestMethod]
    public void AStartGameWithFewerWaitScreenRemovesFailsThePatchInsteadOfLeavingTheJoinStuck()
    {
        CodeInstruction remove = new(MainGameController_StartGame_Patch.INJECTION_OPCODE, MainGameController_StartGame_Patch.INJECTION_OPERAND);
        List<CodeInstruction> twoRemoves = [remove, new CodeInstruction(OpCodes.Nop), remove, new CodeInstruction(OpCodes.Ret)];

        Invoking(() => MainGameController_StartGame_Patch.Transpiler(MainGameController_StartGame_Patch.TARGET_METHOD, twoRemoves).ToList())
            .Should().Throw<InvalidOperationException>().WithMessage("*2 calls*");
    }
}
