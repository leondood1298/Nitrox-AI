using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The looping sounds of entities that play right now, sent to a player who joins. Without it a joiner would not hear
///     a loop (a heated knife, a gun terminal, a fabricator) that started before it came.
/// </summary>
[Serializable]
public class FMODActiveLoops : Packet
{
    public List<Loop> Loops { get; }

    public FMODActiveLoops(List<Loop> loops)
    {
        Loops = loops;
    }

    public override string ToString() => $"[{nameof(FMODActiveLoops)} {Loops.Count} loops]";

    [Serializable]
    public class Loop
    {
        public NitroxId Id { get; }
        public string AssetPath { get; }
        public float Volume { get; }

        public Loop(NitroxId id, string assetPath, float volume)
        {
            Id = id;
            AssetPath = assetPath;
            Volume = volume;
        }
    }
}
