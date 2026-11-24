using System;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The sending player entered a base or vehicle, which secures everything the player carries: none of it is lost when
///     the player dies (Inventory.SecureItems).
/// </summary>
[Serializable]
public class PlayerItemsSecured : Packet
{
}
