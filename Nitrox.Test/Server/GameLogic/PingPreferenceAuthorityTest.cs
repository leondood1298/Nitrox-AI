using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Players;

[TestClass]
public sealed class PingPreferenceAuthorityTest
{
    [TestMethod]
    public void PreferenceForAKnownKeyIsReplaced()
    {
        Dictionary<string, PingInstancePreference> preferences = new() { ["EscapePod13"] = new(1, true) };

        Assert.IsTrue(PingPreferenceAuthority.TryAccept(preferences, "EscapePod13", false, 3));

        Assert.AreEqual(3, preferences["EscapePod13"].Color);
        Assert.IsFalse(preferences["EscapePod13"].Visible);
    }

    [TestMethod]
    public void EmptyOrOverlongKeyIsNotKept()
    {
        Dictionary<string, PingInstancePreference> preferences = [];

        Assert.IsFalse(PingPreferenceAuthority.TryAccept(preferences, "", true, 0));
        Assert.IsFalse(PingPreferenceAuthority.TryAccept(preferences, null, true, 0));
        Assert.IsFalse(PingPreferenceAuthority.TryAccept(preferences, new string('x', PingPreferenceAuthority.MAX_KEY_LENGTH + 1), true, 0));
        Assert.AreEqual(0, preferences.Count);
    }

    [TestMethod]
    public void NoNewKeyIsKeptOnceThePlayerHasTheMaximumButKnownOnesStillChange()
    {
        Dictionary<string, PingInstancePreference> preferences = [];
        for (int i = 0; i < PingPreferenceAuthority.MAX_ENTRIES; i++)
        {
            preferences[$"key{i}"] = new(0, true);
        }

        Assert.IsFalse(PingPreferenceAuthority.TryAccept(preferences, "one too many", true, 0));
        Assert.IsTrue(PingPreferenceAuthority.TryAccept(preferences, "key0", false, 2));
        Assert.AreEqual(PingPreferenceAuthority.MAX_ENTRIES, preferences.Count);
    }

    [TestMethod]
    public void PreferenceOfAnEntityThatIsGoneIsRemovedAndStorySignalsStay()
    {
        NitroxId beaconThatExists = new();
        NitroxId beaconThatIsGone = new();
        Dictionary<string, PingInstancePreference> preferences = new()
        {
            [beaconThatExists.ToString()] = new(1, true),
            [beaconThatIsGone.ToString()] = new(2, false),
            ["EscapePod13"] = new(3, false)
        };

        int removed = PingPreferenceAuthority.RemoveGone(preferences, id => id == beaconThatExists);

        Assert.AreEqual(1, removed);
        CollectionAssert.AreEquivalent(new[] { beaconThatExists.ToString(), "EscapePod13" }, new List<string>(preferences.Keys));
    }
}
