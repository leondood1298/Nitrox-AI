using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Add/remove <see cref="CyclopsDamagePoint" />s and <see cref="Fire" />s to match the <see cref="CyclopsDamage" />
///     packet received
/// </summary>
internal sealed class CyclopsDamageProcessor(CyclopsDamageSync damageSync) : IClientPacketProcessor<CyclopsDamage>
{
    private readonly CyclopsDamageSync damageSync = damageSync;

    public Task Process(ClientProcessorContext context, CyclopsDamage packet)
    {
        SubRoot subRoot = NitroxEntity.RequireObjectFrom(packet.Id).GetComponent<SubRoot>();

        using (PacketSuppressor<CyclopsDamagePointRepaired>.Suppress())
        {
            damageSync.SetActiveDamagePoints(subRoot, packet.DamagePointIndexes);
        }

        using (PacketSuppressor<FireDoused>.Suppress())
        {
            damageSync.SetActiveRoomFires(subRoot, packet.RoomFires);
        }

        LiveMixin subHealth = subRoot.gameObject.RequireComponent<LiveMixin>();

        float oldHPPercent = subRoot.oldHPPercent;

        // Client side noises. Not necessary for keeping the health synced
        if (subHealth.GetHealthFraction() < 0.5f && oldHPPercent >= 0.5f)
        {
            subRoot.voiceNotificationManager.PlayVoiceNotification(subRoot.hullLowNotification);
        }
        else if (subHealth.GetHealthFraction() < 0.25f && oldHPPercent >= 0.25f)
        {
            subRoot.voiceNotificationManager.PlayVoiceNotification(subRoot.hullCriticalNotification);
        }

        using (PacketSuppressor<CyclopsDamage>.Suppress())
        {
            // Not necessary, but used by above code whenever damage is done
            subRoot.oldHPPercent = subHealth.GetHealthFraction();

            // Apply the actual health changes
            subRoot.gameObject.RequireComponent<LiveMixin>().health = packet.SubHealth;
            subRoot.gameObject.RequireComponentInChildren<CyclopsExternalDamageManager>().subLiveMixin.health = packet.DamageManagerHealth;
            subRoot.gameObject.RequireComponent<SubFire>().liveMixin.health = packet.SubFireHealth;
        }
        return Task.CompletedTask;
    }
}
