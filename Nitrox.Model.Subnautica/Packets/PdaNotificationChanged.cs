using System;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player's PDA got an unread marker or lost one because the player looked at it. Each player has their own.
/// </summary>
[Serializable]
public class PdaNotificationChanged : Packet
{
    public PdaNotification Notification { get; }
    public bool Unread { get; }

    public PdaNotificationChanged(PdaNotification notification, bool unread)
    {
        Notification = notification;
        Unread = unread;
    }
}
