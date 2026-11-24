using System;
using System.Collections.Generic;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using Nitrox.Model.Packets;

namespace NitroxClient.Helpers
{
    /// <summary>
    ///     Sends at most one packet per key and throttle time: the first at once, later ones when the time since the last
    ///     one is up, each replacing (or merging into) the one still waiting.
    /// </summary>
    /// <remarks>
    ///     A key is forgotten once its time is up with nothing waiting. Kept forever, as before, every creature this game
    ///     ever simulated left an entry that <see cref="Update" /> went through every frame.
    /// </remarks>
    public class ThrottledPacketSender
    {
        private readonly Dictionary<object, ThrottledPacket> throttledPackets = new();
        private readonly List<object> expiredKeys = new();
        private readonly IPacketSender packetSender;

        public ThrottledPacketSender(IPacketSender packetSender)
        {
            this.packetSender = packetSender;
        }

        /// <summary>The time now; replaced by tests.</summary>
        internal Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        /// <summary>How many keys are held, waiting or within their throttle time.</summary>
        internal int Count => throttledPackets.Count;

        public void Update()
        {
            DateTime now = Clock();
            foreach (KeyValuePair<object, ThrottledPacket> pair in throttledPackets)
            {
                ThrottledPacket throttledPacket = pair.Value;
                if (throttledPacket.SendTime > now)
                {
                    continue;
                }
                if (throttledPacket.WasSend)
                {
                    expiredKeys.Add(pair.Key);
                    continue;
                }

                if (packetSender.Send(throttledPacket.Packet))
                {
                    throttledPacket.MarkSent(now);
                }
            }

            foreach (object key in expiredKeys)
            {
                throttledPackets.Remove(key);
            }
            expiredKeys.Clear();
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
            DateTime now = Clock();

            if (throttledPackets.TryGetValue(dedupeKey, out ThrottledPacket throttledPacket))
            {
                if (merge != null && !throttledPacket.WasSend && throttledPacket.Packet is T pending)
                {
                    packet = merge(pending, packet);
                }
                throttledPacket.ReplacePacket(packet, throttleTime);
                return true;
            }

            throttledPacket = new(packet, throttleTime, now);
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
            /// <summary>When a waiting packet may go, or, once sent, when the next one may go at once again.</summary>
            public DateTime SendTime { get; private set; }

            public Packet Packet { get; private set; }
            public bool WasSend { get; set; }
            private float throttleTime;

            public ThrottledPacket(Packet packet, float throttleTime, DateTime now)
            {
                this.throttleTime = throttleTime;
                SendTime = now.AddSeconds(throttleTime);
                Packet = packet;
            }

            /// <summary>
            ///     The waiting packet went out: the next one waits a whole throttle time from now.
            /// </summary>
            public void MarkSent(DateTime now)
            {
                WasSend = true;
                SendTime = now.AddSeconds(throttleTime);
            }

            public void ReplacePacket(Packet packet, float throttleTime)
            {
                Packet = packet;
                this.throttleTime = throttleTime;
                // Within the time of the last packet sent, this one waits until that time is up
                WasSend = false;
            }
        }
    }
}
