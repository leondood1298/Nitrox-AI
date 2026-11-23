using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class BasePowerRelay_PowerDownEvent_PatchTest
{
    [TestMethod]
    public void PatchTargetsThePowerDownEventOfABasePowerRelay()
    {
        MethodInfo target = (MethodInfo)typeof(BasePowerRelay_PowerDownEvent_Patch).GetField("TARGET_METHOD", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.AreEqual(typeof(BasePowerRelay), target.DeclaringType);
        Assert.AreEqual(typeof(PowerRelay), target.GetParameters().Single().ParameterType, "The game calls it with the relay whose power changed.");
    }
}
