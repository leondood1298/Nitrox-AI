using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class uGUI_MapRoomScanner_Subscription_PatchTest
{
    [TestMethod]
    [DataRow(typeof(uGUI_MapRoomScanner_Start_Patch))]
    [DataRow(typeof(uGUI_MapRoomScanner_OnDisable_Patch))]
    public void PatchTargetsExistInCurrentGameAssembly(Type patchType)
    {
        FieldInfo targetField = patchType.GetField("TARGET_METHOD", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(targetField);
        Assert.IsNotNull(targetField.GetValue(null));
    }
}
