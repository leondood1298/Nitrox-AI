using NitroxClient.Communication.Abstract;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic
{
    public class SeamothModulesEvent
    {
        private readonly IPacketSender packetSender;

        public SeamothModulesEvent(IPacketSender packetSender)
        {
            this.packetSender = packetSender;
        }

        public void BroadcastElectricalDefense(TechType techType, int slotID, SeaMoth instance)
        {
            if (!instance.TryGetIdOrWarn(out NitroxId id))
            {
                return;
            }

            if (techType == TechType.SeamothElectricalDefense)
            {
                // Read before SeaMoth.OnUpgradeModuleUse runs: the game resets the slot's charge once the module is used
                SeamothModulesAction changed = new(techType.ToDto(), slotID, id, instance.quickSlotCharge[slotID], instance.GetSlotCharge(slotID));
                packetSender.Send(changed);
            }
        }
    }
}
