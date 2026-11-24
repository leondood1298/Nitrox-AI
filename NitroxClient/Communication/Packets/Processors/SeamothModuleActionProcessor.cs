using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SeamothModuleActionProcessor : IClientPacketProcessor<SeamothModulesAction>
{
    public Task Process(ClientProcessorContext context, SeamothModulesAction packet)
    {
        using (PacketSuppressor<SeamothModulesAction>.Suppress())
        {
            if (!NitroxEntity.TryGetComponentFrom(packet.Id, out SeaMoth seamoth))
            {
                Log.Error($"[{nameof(SeamothModuleActionProcessor)}] Couldn't find SeaMoth component on {packet.Id}");
                return Task.CompletedTask;
            }

            switch (packet.TechType.ToUnity())
            {
                case TechType.SeamothElectricalDefense:
                {
                    // The pilot's charge: this game's own charge of the slot is zero (only the pilot holds the key down)
                    GameObject gameObject = Utils.SpawnZeroedAt(seamoth.seamothElectricalDefensePrefab, seamoth.transform);
                    ElectricalDefense component = gameObject.GetComponent<ElectricalDefense>();
                    component.charge = packet.Charge;
                    component.chargeScalar = packet.ChargeScalar;
                    component.defenseSound = null; // Disable sound in Start(). Sound is synced over general Nitrox FMOD system.
                    break;
                }
            }
        }
        return Task.CompletedTask;
    }
}
