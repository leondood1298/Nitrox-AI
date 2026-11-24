using System.Reflection.Emit;
using HarmonyLib;
using NitroxPatcher.Patches.Dynamic;
using NitroxTest.Patcher;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class WaterParkCreature_ManagedUpdate_PatchTest
{
    [TestMethod]
    public void GrownCreatureIsReportedWhileItStillKnowsItsContainment()
    {
        List<CodeInstruction> transformed = WaterParkCreature_ManagedUpdate_Patch.Transpiler(PatchTestHelper.GetInstructionsFromMethod(WaterParkCreature_ManagedUpdate_Patch.TARGET_METHOD)).ToList();

        MethodInfo callback = typeof(WaterParkCreature_ManagedUpdate_Patch).GetMethod(nameof(WaterParkCreature_ManagedUpdate_Patch.CreatureDestroyedCallback));
        MethodInfo setWaterPark = typeof(WaterParkItem).GetMethod(nameof(WaterParkItem.SetWaterPark));
        int callbackIndex = transformed.FindIndex(instruction => instruction.Calls(callback));
        int forgetIndex = Enumerable.Range(1, transformed.Count - 1).First(i => transformed[i].Calls(setWaterPark) && transformed[i - 1].opcode == OpCodes.Ldnull);

        callbackIndex.Should().BePositive();
        forgetIndex.Should().BeGreaterThan(callbackIndex, "SetWaterPark(null) forgets the containment that decides who reports the grown creature");
    }
}
