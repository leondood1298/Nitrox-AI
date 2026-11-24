using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class PingPreferencesTest
{
    [TestMethod]
    public void AChangeMadeAfterJoiningIsWhatIsAppliedNext()
    {
        PingPreferences preferences = new();
        preferences.Load(new Dictionary<string, PingInstancePreference> { ["beacon"] = new(1, true) });

        preferences.Remember("beacon", false, 4);

        preferences.TryGet("beacon", out PingInstancePreference preference).Should().BeTrue();
        preference.Color.Should().Be(4);
        preference.Visible.Should().BeFalse();
    }

    [TestMethod]
    public void LoadingReplacesWhatAnEarlierWorldLeft()
    {
        PingPreferences preferences = new();
        preferences.Remember("EscapePod13", false, 2);

        preferences.Load(new Dictionary<string, PingInstancePreference> { ["beacon"] = new(1, true) });

        preferences.TryGet("EscapePod13", out _).Should().BeFalse();
        preferences.TryGet("beacon", out _).Should().BeTrue();
    }

    [TestMethod]
    public void TheSnapshotFromTheServerIsCopiedNotShared()
    {
        Dictionary<string, PingInstancePreference> snapshot = new() { ["beacon"] = new(1, true) };
        PingPreferences preferences = new();
        preferences.Load(snapshot);

        preferences.Remember("beacon", false, 3);

        snapshot["beacon"].Color.Should().Be(1);
    }
}
