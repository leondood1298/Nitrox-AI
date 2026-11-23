using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class BasePowerRelay_PowerUpEvent_PatchTest
{
    [TestMethod]
    public void PatchTargetsThePowerUpEventOfABasePowerRelay()
    {
        MethodInfo target = (MethodInfo)typeof(BasePowerRelay_PowerUpEvent_Patch).GetField("TARGET_METHOD", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.AreEqual(typeof(BasePowerRelay), target.DeclaringType);
        Assert.AreEqual(typeof(PowerRelay), target.GetParameters().Single().ParameterType, "The game calls it with the relay whose power changed.");
    }
}
