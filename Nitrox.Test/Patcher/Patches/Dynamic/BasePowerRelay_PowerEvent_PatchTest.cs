using NitroxClient.GameLogic;
using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class BasePowerRelay_PowerEvent_PatchTest
{
    [TestMethod]
    [DataRow(typeof(BasePowerRelay_Start_Patch), false)]
    [DataRow(typeof(BasePowerRelay_PowerDownEvent_Patch), true)]
    [DataRow(typeof(BasePowerRelay_PowerUpEvent_Patch), true)]
    public void PatchTargetsExistInCurrentGameAssembly(Type patchType, bool takesPowerRelay)
    {
        FieldInfo targetField = patchType.GetField("TARGET_METHOD",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.IsNotNull(targetField);
        MethodInfo targetMethod = (MethodInfo)targetField.GetValue(null)!;
        Assert.IsNotNull(targetMethod);
        if (takesPowerRelay)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(PowerRelay), parameters[0].ParameterType);
        }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void TracksOnlyMultiplayerRelaysCreatedDuringReconciliation(bool multiplayerActive,
        bool initialSyncCompleted, bool expected)
    {
        Assert.AreEqual(expected, BasePowerRelayAudioSuppressor.ShouldTrack(multiplayerActive, initialSyncCompleted));
    }

    [TestMethod]
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
        Assert.AreEqual(expected, BasePowerRelayAudioSuppressor.ShouldSuppress(tracked, initialSyncCompleted,
            currentTime, suppressUntil));
    }
}
