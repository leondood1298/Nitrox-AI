using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class uGUI_MapRoomScanner_Subscription_PatchTest
{
    [DataTestMethod]
    [DataRow("START")]
    [DataRow("ON_DISABLE")]
    public void PatchTargetsExistInCurrentGameAssembly(string fieldName)
    {
        FieldInfo targetField = typeof(uGUI_MapRoomScanner_Subscription_Patch).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(targetField);
        Assert.IsNotNull(targetField.GetValue(null));
    }
}
