using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.FMOD;

namespace Nitrox.Test.Client.GameLogic.FMOD;

[TestClass]
public sealed class ReplayedSoundScopeTest
{
    [TestMethod]
    public void WhileARepeatedActionIsHandledNoSoundIsSent()
    {
        ReplayedSoundScope.IsActive.Should().BeFalse();
        using (ReplayedSoundScope.Enter())
        {
            ReplayedSoundScope.IsActive.Should().BeTrue();
            PacketSuppressor<FMODAssetPacket>.IsSuppressed.Should().BeTrue();
            PacketSuppressor<FMODEventInstancePacket>.IsSuppressed.Should().BeTrue();
            PacketSuppressor<FMODCustomEmitterPacket>.IsSuppressed.Should().BeTrue();
            PacketSuppressor<FMODStudioEmitterPacket>.IsSuppressed.Should().BeTrue();
        }
        ReplayedSoundScope.IsActive.Should().BeFalse();
        PacketSuppressor<FMODAssetPacket>.IsSuppressed.Should().BeFalse();
    }

    [TestMethod]
    public void ScopesNest()
    {
        using (ReplayedSoundScope.Enter())
        {
            using (ReplayedSoundScope.Enter())
            {
            }
            ReplayedSoundScope.IsActive.Should().BeTrue();
            PacketSuppressor<FMODAssetPacket>.IsSuppressed.Should().BeTrue();
        }
        ReplayedSoundScope.IsActive.Should().BeFalse();
    }
}
