using System.Collections.Generic;
using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

[TestClass]
public sealed class SoundLoopsTest
{
    private const string LOOP = "event:/tools/knife/heat_idle";
    private static readonly SessionId starter = 1;
    private static readonly SessionId other = 2;

    [TestMethod]
    public void AJoinerIsToldTheLoopsThatPlay()
    {
        SoundLoops loops = new(new RecordingPacketSender());
        NitroxId knife = new();

        loops.Started(starter, knife, LOOP, 0.7f, Stop(knife));

        FMODActiveLoops active = loops.GetActiveLoops();
        active.Loops.Should().ContainSingle();
        active.Loops[0].Id.Should().Be(knife);
        active.Loops[0].AssetPath.Should().Be(LOOP);
        active.Loops[0].Volume.Should().Be(0.7f);
    }

    [TestMethod]
    public void AStoppedLoopIsForgotten()
    {
        SoundLoops loops = new(new RecordingPacketSender());
        NitroxId knife = new();

        loops.Started(starter, knife, LOOP, 1f, Stop(knife));
        loops.Stopped(knife, LOOP);

        loops.GetActiveLoops().Loops.Should().BeEmpty();
    }

    [TestMethod]
    public void TheLoopsOfAPlayerWhoLeavesAreStoppedForTheOthers()
    {
        SoundLoops loops = new(new RecordingPacketSender());
        NitroxId knife = new();
        NitroxId terminal = new();
        Packet knifeStop = Stop(knife);

        loops.Started(starter, knife, LOOP, 1f, knifeStop);
        loops.Started(other, terminal, LOOP, 1f, Stop(terminal));

        List<Packet> stops = loops.TakeStopsOf(starter);

        stops.Should().ContainSingle().Which.Should().BeSameAs(knifeStop);
        loops.GetActiveLoops().Loops.Should().ContainSingle().Which.Id.Should().Be(terminal);
    }

    private static Packet Stop(NitroxId id) => new FMODEventInstancePacket(id, false, LOOP, NitroxVector3.Zero, 0f, true);

    private sealed class RecordingPacketSender : IPacketSender
    {
        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet => ValueTask.CompletedTask;
    }
}
