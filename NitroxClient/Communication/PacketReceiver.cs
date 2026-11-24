using System;
using System.Collections.Generic;
using Nitrox.Model.Packets;

namespace NitroxClient.Communication;

public class PacketReceiver
{
    private readonly Queue<Packet> receivedPackets = new(16);
    private readonly object receivedPacketsLock = new();

    public void Add(Packet packet)
    {
        lock (receivedPacketsLock)
        {
            receivedPackets.Enqueue(packet);
        }
    }

    public Packet? GetNextPacket()
    {
        lock (receivedPacketsLock)
        {
            return receivedPackets.Count == 0 ? null : receivedPackets.Dequeue();
        }
    }

    /// <summary>
    ///     Applies an operation on each packet waiting to be processed and removes it from the queue.
    /// </summary>
    public void ConsumePackets<TExtra>(Action<Packet, TExtra> consumer, TExtra extraParameter) => ConsumePackets(consumer, extraParameter, static _ => false);

    /// <summary>
    ///     Like <see cref="ConsumePackets{TExtra}(Action{Packet, TExtra}, TExtra)" />, but stops once
    ///     <paramref name="outOfTime" /> says so, leaving the rest queued in order for the next call. At least one packet is
    ///     handled per call, so that the queue always moves.
    /// </summary>
    public void ConsumePackets<TExtra>(Action<Packet, TExtra> consumer, TExtra extraParameter, Func<TExtra, bool> outOfTime)
    {
        Packet? packet = GetNextPacket();
        while (packet != null)
        {
            consumer(packet, extraParameter);
            if (outOfTime(extraParameter))
            {
                return;
            }
            packet = GetNextPacket();
        }
    }

    public int Count
    {
        get
        {
            lock (receivedPacketsLock)
            {
                return receivedPackets.Count;
            }
        }
    }
}
