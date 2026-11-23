using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata
{
    /// <remarks>
    ///     A door that has to be laser cut open carries its <c>Sealed</c> on the same object, so the cut state shares this
    ///     metadata: an entity has one metadata slot, and two kinds sent for one door would keep replacing each other.
    /// </remarks>
    [Serializable]
    [DataContract]
    public class StarshipDoorMetadata : EntityMetadata
    {
        [DataMember(Order = 1)]
        public bool DoorLocked { get; }

        [DataMember(Order = 2)]
        public bool DoorOpen { get; }

        /// <summary>
        ///     Whether the door still has to be cut open. Only used by doors that can be cut.
        /// </summary>
        [DataMember(Order = 3)]
        public bool Sealed { get; }

        /// <summary>
        ///     How far the cutting has gone. Only used by doors that can be cut.
        /// </summary>
        [DataMember(Order = 4)]
        public float OpenedAmount { get; }

        [IgnoreConstructor]
        protected StarshipDoorMetadata()
        {
            //Constructor for serialization. Has to be "protected" for json serialization.
        }

        public StarshipDoorMetadata(bool doorLocked, bool doorOpen, bool @sealed, float openedAmount)
        {
            DoorLocked = doorLocked;
            DoorOpen = doorOpen;
            Sealed = @sealed;
            OpenedAmount = openedAmount;
        }

        public override string ToString()
        {
            return $"[StarshipDoorMetadata DoorLocked: {DoorLocked} DoorOpen: {DoorOpen} Sealed: {Sealed} OpenedAmount: {OpenedAmount}]";
        }
    }
}
