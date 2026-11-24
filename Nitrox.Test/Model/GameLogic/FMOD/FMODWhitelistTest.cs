using Nitrox.Model;
using Nitrox.Model.GameLogic.FMOD;

namespace Nitrox.Test.Model.GameLogic.FMOD;

/// <summary>
///     The sound whitelist's choices that the code relies on. Radii come from the game's banks (the events' maximum
///     distance, read through the game's own fmodstudio.dll), except where noted.
/// </summary>
[TestClass]
public sealed class FMODWhitelistTest
{
    [TestMethod]
    [DataRow("event:/sub/cyclops/outer_hatch_open", 50f)]
    [DataRow("event:/sub/cyclops/outer_hatch_close", 50f)]
    [DataRow("event:/sub/exo/land", 20f)]
    [DataRow("event:/sub/exo/land_precursor", 20f)]
    [DataRow("event:/sub/exo/hook_loop", 20f)]
    [DataRow("event:/sub/exo/hook_shoot", 20f)]
    [DataRow("event:/sub/exo/hook_hit", 85f)]
    [DataRow("event:/sub/exo/claw_punch", 20f)]
    [DataRow("event:/sub/seamoth/crush_damage", 20f)]
    [DataRow("event:/sub_module/fabricator/open", 8.2f)]
    [DataRow("event:/sub_module/workbench/close", 8.2f)]
    [DataRow("event:/sub/base/make_coffee", 21f)]
    [DataRow("event:/tools/knife/hit", 20f)]
    [DataRow("event:/tools/flare/on_loop", 9.8f)]
    public void RadiiAreTheEventsOwnMaximumDistance(string path, float radius)
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);
        whitelist.TryGetSoundData(path, out SoundData data).Should().BeTrue(path);
        data.Radius.Should().BeApproximately(radius, 0.01f, path);
    }

    [TestMethod]
    public void TheCyclopsHornCarriesFarOnPurpose()
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);
        whitelist.TryGetSoundData("event:/sub/cyclops/horn", out SoundData data).Should().BeTrue();
        data.Radius.Should().Be(500f, "the event's own 20 m is too short to call other players");
    }

    [TestMethod]
    [DataRow("event:/sub/seamoth/torpedo_fire")]
    [DataRow("event:/sub_module/fabricator/build")]
    [DataRow("event:/sub_module/fabricator/build_long_loop")]
    [DataRow("event:/sub/base/seamoth_dock")]
    [DataRow("event:/sub/exo/dock_exo_moonpool")]
    [DataRow("event:/sub/seamoth/dock")]
    public void SoundsEveryGamePlaysForItselfAreNotSent(string path)
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);
        whitelist.TryGetSoundData(path, out SoundData data).Should().BeTrue(path);
        data.IsWhitelisted.Should().BeFalse($"{path} is played by every game for a synced cause; sending it as well doubles it");
        data.Radius.Should().BeGreaterThan(0f, "a game fades it by the distance to the cause");
    }

    [TestMethod]
    [DataRow("event:/player/eat")]
    [DataRow("event:/player/gunterminal_use_healed")]
    [DataRow("event:/sub/cyclops/enter_hatch")]
    [DataRow("event:/sub/pod/climb_out_top_first_use")]
    [DataRow("event:/sub/rocket/use_lift")]
    public void LongFirstPersonSoundsOfOtherPlayersAreNotSent(string path)
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);
        whitelist.TryGetSoundData(path, out SoundData data).Should().BeTrue(path);
        data.IsWhitelisted.Should().BeFalse(path);
    }

    [TestMethod]
    [DataRow("event:/player/jump")]
    [DataRow("event:/player/bubbles")]
    [DataRow("event:/tools/knife/swing")]
    [DataRow("event:/tools/scanner/scan_loop")]
    public void ShortFirstPersonSoundsAreSentWithinTenMetres(string path)
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);
        whitelist.TryGetSoundData(path, out SoundData data).Should().BeTrue(path);
        data.IsWhitelisted.Should().BeTrue(path);
        data.Radius.Should().BeLessThanOrEqualTo(10f, path);
    }

    [TestMethod]
    [DataRow("event:/tools/stasis_gun/charge")]
    [DataRow("event:/tools/flare/throw")]
    [DataRow("event:/tools/builder/loop")]
    [DataRow("event:/tools/builder/place")]
    [DataRow("event:/sub/seamoth/sonar_loop")]
    public void SoundsOnPathsNowRelayedAreSent(string path)
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);
        whitelist.IsWhitelisted(path).Should().BeTrue(path);
    }
}
