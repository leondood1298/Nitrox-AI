using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class BasePowerRelay_Start_PatchTest
{
    [TestMethod]
    public void PatchTargetsTheStartOfABasePowerRelay()
    {
        MethodInfo target = (MethodInfo)typeof(BasePowerRelay_Start_Patch).GetField("TARGET_METHOD", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.AreEqual(typeof(BasePowerRelay), target.DeclaringType);
        Assert.AreEqual(0, target.GetParameters().Length);
    }
}
