using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The looping sounds of entities that play right now and who started them. A loop is only announced when it starts,
///     so a player who joins later would never hear it, and one whose starter leaves would never get its stop.
/// </summary>
internal sealed class SoundLoops(IPacketSender packetSender) : ISessionCleaner
{
    private readonly Dictionary<(NitroxId Id, string AssetPath), Entry> loops = [];
    private readonly Lock loopsLock = new();
    private readonly IPacketSender packetSender = packetSender;

    public void Started(SessionId starter, NitroxId id, string assetPath, float volume, Packet stop)
    {
        lock (loopsLock)
        {
            loops[(id, assetPath)] = new Entry(starter, volume, stop);
        }
    }

    public void Stopped(NitroxId id, string assetPath)
    {
        lock (loopsLock)
        {
            loops.Remove((id, assetPath));
        }
    }

    public FMODActiveLoops GetActiveLoops()
    {
        lock (loopsLock)
        {
            return new FMODActiveLoops(loops.Select(pair => new FMODActiveLoops.Loop(pair.Key.Id, pair.Key.AssetPath, pair.Value.Volume)).ToList());
        }
    }

    /// <summary>
    ///     Takes out the loops that <paramref name="starter" /> started and gives the stops to send for them.
    /// </summary>
    public List<Packet> TakeStopsOf(SessionId starter)
    {
        lock (loopsLock)
        {
            List<(NitroxId, string)> keys = loops.Where(pair => pair.Value.Starter == starter).Select(pair => pair.Key).ToList();
            List<Packet> stops = keys.Select(key => loops[key].Stop).ToList();
            foreach ((NitroxId, string) key in keys)
            {
                loops.Remove(key);
            }
            return stops;
        }
    }

    public async Task OnEventAsync(ISessionCleaner.Args args)
    {
        foreach (Packet stop in TakeStopsOf(args.Session.Id))
        {
            await packetSender.SendPacketToOthersAsync(stop, args.Session.Id);
        }
    }

    private sealed record Entry(SessionId Starter, float Volume, Packet Stop);
}
