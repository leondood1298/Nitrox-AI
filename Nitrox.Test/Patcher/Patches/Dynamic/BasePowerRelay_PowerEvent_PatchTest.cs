using System.Reflection;
using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class BasePowerRelay_PowerEvent_PatchTest
{
    [DataTestMethod]
    [DataRow("START")]
    [DataRow("POWER_DOWN_EVENT")]
    [DataRow("POWER_UP_EVENT")]
    public void PatchTargetsExistInCurrentGameAssembly(string fieldName)
    {
        FieldInfo targetField = typeof(BasePowerRelay_PowerEvent_Patch).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.IsNotNull(targetField);
        MethodInfo targetMethod = (MethodInfo)targetField.GetValue(null)!;
        Assert.IsNotNull(targetMethod);
        if (fieldName != "START")
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(PowerRelay), parameters[0].ParameterType);
        }
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void TracksOnlyMultiplayerRelaysCreatedDuringReconciliation(bool multiplayerActive,
        bool initialSyncCompleted, bool expected)
    {
        Assert.AreEqual(expected, BasePowerRelay_PowerEvent_Patch.ShouldTrack(multiplayerActive, initialSyncCompleted));
    }

    [DataTestMethod]
    [DataRow(false, false, 10f, 12f, false)]
    [DataRow(false, true, 10f, 12f, false)]
    [DataRow(true, false, 10f, 12f, true)]
    [DataRow(true, true, 10f, 12f, true)]
    [DataRow(true, true, 11.99f, 12f, true)]
    [DataRow(true, true, 12f, 12f, false)]
    [DataRow(true, true, 13f, 12f, false)]
    public void SuppressesTrackedLoadRelayOnlyUntilScaledTimeBoundary(bool tracked, bool initialSyncCompleted,
        float currentTime, float suppressUntil, bool expected)
    {
        Assert.AreEqual(expected, BasePowerRelay_PowerEvent_Patch.ShouldSuppress(tracked, initialSyncCompleted,
            currentTime, suppressUntil));
    }
}
