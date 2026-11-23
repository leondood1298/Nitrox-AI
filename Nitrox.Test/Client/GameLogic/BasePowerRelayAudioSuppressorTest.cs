using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The voice that announces a base losing power or getting it back stays silent for the relays that are created
///     while a player joins, until their restored power state has settled.
/// </summary>
[TestClass]
public sealed class BasePowerRelayAudioSuppressorTest
{
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
