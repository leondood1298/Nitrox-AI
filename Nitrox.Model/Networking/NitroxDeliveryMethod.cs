namespace Nitrox.Model.Networking;

public static class NitroxDeliveryMethod
{
    /// <summary>
    ///     The most a serialized <see cref="DeliveryMethod.UNRELIABLE_SEQUENCED" /> packet may take. It is sent as one
    ///     datagram, which LiteNetLib refuses above its smallest MTU (1024) less its CRC (4), its header (4) and Nitrox's
    ///     length (4): 1012 bytes. A little is kept spare.
    /// </summary>
    public const int MAX_UNRELIABLE_PACKET_BYTES = 1000;

    public enum DeliveryMethod : byte
    {
        /// <summary>
        /// <inheritdoc cref="LiteNetLib.DeliveryMethod.Sequenced"/>
        /// </summary>
        UNRELIABLE_SEQUENCED = LiteNetLib.DeliveryMethod.Sequenced,
        /// <summary>
        /// <inheritdoc cref="LiteNetLib.DeliveryMethod.ReliableUnordered"/>
        /// </summary>
        RELIABLE_UNORDERED = LiteNetLib.DeliveryMethod.ReliableUnordered,
        /// <summary>
        /// <inheritdoc cref="LiteNetLib.DeliveryMethod.ReliableOrdered"/>
        /// </summary>
        RELIABLE_ORDERED = LiteNetLib.DeliveryMethod.ReliableOrdered,
        /// <summary>
        /// <inheritdoc cref="LiteNetLib.DeliveryMethod.ReliableSequenced"/>
        /// </summary>
        RELIABLE_ORDERED_LAST = LiteNetLib.DeliveryMethod.ReliableSequenced,
    }

    public static LiteNetLib.DeliveryMethod ToLiteNetLib(DeliveryMethod deliveryMethod)
    {
        switch (deliveryMethod)
        {
            case DeliveryMethod.UNRELIABLE_SEQUENCED:
            case DeliveryMethod.RELIABLE_ORDERED_LAST:
            case DeliveryMethod.RELIABLE_ORDERED:
            case DeliveryMethod.RELIABLE_UNORDERED:
                return (LiteNetLib.DeliveryMethod)deliveryMethod;

            default:
                return LiteNetLib.DeliveryMethod.ReliableOrdered;
        }
    }
}
