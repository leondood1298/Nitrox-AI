using System;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class RadioPlayPendingMessage : Packet
{
    /// <summary>
    ///     Key of the radio message that was played. Queues can differ between games, so the receivers play this message
    ///     and not whatever is first in their own queue.
    /// </summary>
    public string MessageKey { get; }

    public RadioPlayPendingMessage(string messageKey)
    {
        MessageKey = messageKey;
    }
}
