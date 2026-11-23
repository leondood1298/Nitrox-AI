using System;
using System.Collections.Generic;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.Helpers
{
    public class ThrottledPacketSender
    {
        private readonly Dictionary<object, ThrottledPacket> throttledPackets = new();
        private readonly IPacketSender packetSender;

        public ThrottledPacketSender(IPacketSender packetSender)
        {
            this.packetSender = packetSender;
        }

        public void Update()
        {
            foreach (ThrottledPacket throttledPacket in throttledPackets.Values)
            {
                if (throttledPacket.WasSend || throttledPacket.SendTime > DateTime.UtcNow)
                {
                    continue;
                }

                if (packetSender.Send(throttledPacket.Packet))
                {
                    throttledPacket.WasSend = true;
                }
            }
        }

        /// <param name="merge">
        ///     Given the pending packet that was not sent yet and the new one, what to send instead of both. Without it the
        ///     new one replaces the pending one.
        /// </param>
        public bool SendThrottled<T>(T packet, Func<T, object> dedupeMethod, float throttleTime = 0.2f, Func<T, T, T> merge = null) where T : Packet
        {
            if (PacketSuppressor<T>.IsSuppressed)
            {
                return false;
            }

            object dedupeKey = dedupeMethod.Invoke(packet);

            if (throttledPackets.TryGetValue(dedupeKey, out ThrottledPacket throttledPacket))
            {
                if (merge != null && !throttledPacket.WasSend && throttledPacket.Packet is T pending)
                {
                    packet = merge(pending, packet);
                }
                throttledPacket.ReplacePacket(packet, throttleTime);
                return true;
            }

            throttledPacket = new(packet, throttleTime);
            throttledPackets.Add(dedupeKey, throttledPacket);
            packetSender.Send(packet);
            // It's very important to set WasSend to true, otherwise the packet will be sent again in Update()
            throttledPacket.WasSend = true;
            return true;
        }

        public bool RemovePendingPackets(object dedupeKey)
        {
            return throttledPackets.Remove(dedupeKey);
        }

        /// <summary>
        ///     Removes what is held under the key, giving back the packet that was waiting to be sent, if any.
        /// </summary>
        public bool TryTakePendingPacket<T>(object dedupeKey, out T pending) where T : Packet
        {
            pending = null;
            if (!throttledPackets.TryGetValue(dedupeKey, out ThrottledPacket throttledPacket))
            {
                return false;
            }
            throttledPackets.Remove(dedupeKey);
            if (throttledPacket.WasSend || throttledPacket.Packet is not T packet)
            {
                return false;
            }
            pending = packet;
            return true;
        }

        private class ThrottledPacket
        {
            public DateTime SendTime { get; private set; }
            public Packet Packet { get; private set; }
            public bool WasSend { get; set; }

            public ThrottledPacket(Packet packet, float throttleTime)
            {
                SendTime = DateTime.UtcNow.AddSeconds(throttleTime);
                Packet = packet;
            }

            public void ReplacePacket(Packet packet, float throttleTime)
            {
                Packet = packet;

                if (WasSend)
                {
                    SendTime = DateTime.UtcNow.AddSeconds(throttleTime);
                    WasSend = false;
                }
            }
        }
    }
}
