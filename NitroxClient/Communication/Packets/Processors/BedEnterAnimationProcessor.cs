using System.Collections;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.BedSync;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class BedEnterAnimationProcessor(PlayerManager playerManager) : IClientPacketProcessor<BedEnterAnimation>
{
    /// <summary>
    ///     How long to keep trying when the bed or the other player's body is not there yet. This happens right after
    ///     joining, when the server tells us who is already asleep.
    /// </summary>
    private const float RETRY_SECONDS = 15f;

    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, BedEnterAnimation packet)
    {
        if (!TryStart(packet))
        {
            UWE.CoroutineHost.StartCoroutine(StartWhenReady(packet));
        }
        return Task.CompletedTask;
    }

    private IEnumerator StartWhenReady(BedEnterAnimation packet)
    {
        float deadline = Time.realtimeSinceStartup + RETRY_SECONDS;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForSeconds(0.5f);
            if (TryStart(packet))
            {
                yield break;
            }
        }
        Log.Warn($"[{nameof(BedEnterAnimationProcessor)}] Could not show player #{packet.SessionId} lying in bed {packet.BedId}: the bed or the player never became available");
    }

    private bool TryStart(BedEnterAnimation packet)
    {
        if (!NitroxEntity.TryGetObjectFrom(packet.BedId, out GameObject bed) || !bed.TryGetComponent(out RemoteBedController bedController) ||
            !playerManager.TryFind(packet.SessionId, out RemotePlayer remotePlayer) || !remotePlayer.Body || !remotePlayer.Body.activeInHierarchy)
        {
            return false;
        }
        bedController.StartBedAnimation(remotePlayer, packet.AnimationKey);
        return true;
    }
}
