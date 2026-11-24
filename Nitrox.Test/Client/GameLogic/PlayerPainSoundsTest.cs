using Nitrox.Model;
using Nitrox.Model.GameLogic.FMOD;
using NitroxClient.GameLogic.FMOD;
using static NitroxClient.GameLogic.FMOD.PlayerPainSounds;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The pain sound sent to the others is the one <c>PlayerDamageSounds.OnTakeDamage</c> picks.
/// </summary>
[TestClass]
public sealed class PlayerPainSoundsTest
{
    private const int NORMAL = 0;
    private const int RADIATION = 12;
    private const int SMOKE = 16;

    [TestMethod]
    public void NoDamageOrRadiationIsSilent()
    {
        Choose(0f, NORMAL, true, true, false).Should().Be(Pain.NONE);
        Choose(10f, RADIATION, true, true, false).Should().Be(Pain.NONE);
    }

    [TestMethod]
    public void SmokeComesFirst()
    {
        Choose(5f, SMOKE, true, false, true).Should().Be(Pain.SMOKE);
    }

    [TestMethod]
    public void OutOfTheWaterTheSurfaceSoundPlays()
    {
        Choose(5f, NORMAL, true, false, true).Should().Be(Pain.SURFACE);
    }

    [TestMethod]
    public void UnderwaterTheTankDecides()
    {
        Choose(5f, NORMAL, true, true, true).Should().Be(Pain.WITH_TANK);
        Choose(20f, NORMAL, true, true, false).Should().Be(Pain.WITHOUT_TANK);
        Choose(8f, NORMAL, true, true, false).Should().Be(Pain.WITHOUT_TANK_LIGHT);
    }

    /// <summary>
    ///     Every event <see cref="PlayerPainSounds.BeforePain" /> can send, spelled as the game's PlayerSounds in the Main
    ///     scene has them: the paths of the four FMODAssets and the Event string of the FMODUnity emitter of the light pain
    ///     (smoke, surface, with tank, without tank, without tank light).
    /// </summary>
    private static readonly string[] gamePainEvents =
    [
        "event:/player/coughing",
        "event:/player/Pain_surface",
        "event:/player/Pain",
        "event:/player/Pain_no_tank",
        "event:/player/pain_no_tank_light"
    ];

    [TestMethod]
    public void ThePainSoundsHaveARadiusButAreNotRelayedByTheGenericPatches()
    {
        using FMODWhitelist whitelist = FMODWhitelist.Load(GameInfo.Subnautica);

        foreach (string path in gamePainEvents)
        {
            whitelist.TryGetSoundData(path, out SoundData data).Should().BeTrue(path);
            data.Radius.Should().BeGreaterThan(0f, path);
            data.IsWhitelisted.Should().BeFalse($"{path} is sent by PlayerPainSounds; the emitter patches would send it again");
        }
    }
}
