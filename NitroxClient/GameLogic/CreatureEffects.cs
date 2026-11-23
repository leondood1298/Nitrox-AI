using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;
using static Nitrox.Model.Subnautica.Packets.CreatureEffectPlayed;

namespace NitroxClient.GameLogic;

/// <summary>
///     What a creature does beyond moving and biting, decided by the game of the player simulating it and played on every
///     game's copy (<see cref="CreatureEffectPlayed" />):<br />
///     - a Gasopod's pods: every game's copy released its own, when something near it on that game scared it. Only the
///     simulating game decides now, and every game makes the same pods, which hurt whoever is in their gas there<br />
///     - a Crabsquid's EMP blast: it only reached what was on the simulating player's game. Every game casts it now, so it
///     shuts down the vehicle and tools of each player it reaches<br />
///     - a Mesmer's hypnosis: the game only hypnotizes its own player, so a Mesmer hypnotized nobody but its simulating
///     player. It may now pick another player, whose game is told and plays the hypnosis on its copy
/// </summary>
/// <remarks>Unity-bound.</remarks>
public sealed class CreatureEffects(IPacketSender packetSender, SimulationOwnership simulationOwnership)
{
    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;

    private bool applyingReceived;

    /// <summary>
    ///     Called before GasoPod.Update, which releases pods when the Gasopod is scared.
    /// </summary>
    public bool MayReleaseGas(GasoPod gasoPod) => IsDecidedHere(gasoPod.gameObject, out _);

    /// <summary>
    ///     Called before GasoPod.DropGasPods: seeds the game's random numbers and sends the seed, so every game makes the
    ///     same pods. Returns the random state to put back afterwards.
    /// </summary>
    public Random.State BeginGasRelease(GasoPod gasoPod)
    {
        Random.State previous = Random.state;
        if (!applyingReceived && IsDecidedHere(gasoPod.gameObject, out NitroxId gasoPodId) && gasoPodId != null)
        {
            int seed = Random.Range(int.MinValue, int.MaxValue);
            Random.InitState(seed);
            packetSender.Send(new CreatureEffectPlayed(gasoPodId, CreatureEffect.GAS_PODS_RELEASED, seed, Optional.Empty));
        }
        return previous;
    }

    /// <summary>
    ///     Called after GasoPod.DropGasPods. The pods' directions come from the seed, which only holds while they are made.
    /// </summary>
    public void EndGasRelease(Random.State previous)
    {
        if (!applyingReceived)
        {
            Random.state = previous;
        }
    }

    /// <summary>
    ///     Called after EMPAttack.CastEMPBlast.
    /// </summary>
    public void EmpBlastCast(EMPAttack empAttack)
    {
        if (!applyingReceived && IsDecidedHere(empAttack.gameObject, out NitroxId creatureId) && creatureId != null)
        {
            packetSender.Send(new CreatureEffectPlayed(creatureId, CreatureEffect.EMP_BLAST_CAST, 0, Optional.Empty));
        }
    }

    /// <summary>
    ///     Called after PullCreatures.IsValidTarget, which only lets the game's own player be hypnotized: another player's
    ///     body passes the same checks.
    /// </summary>
    public bool IsValidHypnosisTarget(PullCreatures pullCreatures, GameObject target, bool validForTheGame)
    {
        if (validForTheGame || !target || !target.TryGetComponent(out RemotePlayerIdentifier remotePlayerIdentifier))
        {
            return validForTheGame;
        }
        RemotePlayer remotePlayer = remotePlayerIdentifier.RemotePlayer;
        if (!remotePlayer.CanBeAttacked() || remotePlayer.Vehicle || !remotePlayer.LiveMixin || !remotePlayer.LiveMixin.IsAlive())
        {
            return false;
        }
        float distance = Vector3.Distance(target.transform.position, pullCreatures.transform.position);
        return distance <= pullCreatures.maxDistance && distance >= pullCreatures.minDistance && pullCreatures.LooksAtMe(target);
    }

    /// <summary>
    ///     Called after PullCreatures.StartPerform and before PullCreatures.StopPerform on the simulating game: tells the
    ///     hypnotized player's game.
    /// </summary>
    public void HypnosisChanged(PullCreatures pullCreatures, bool started)
    {
        GameObject target = pullCreatures.currentTarget;
        if (applyingReceived || !target || !target.TryGetComponent(out RemotePlayerIdentifier remotePlayerIdentifier) ||
            !IsDecidedHere(pullCreatures.gameObject, out NitroxId creatureId) || creatureId == null)
        {
            return;
        }
        CreatureEffect effect = started ? CreatureEffect.HYPNOSIS_STARTED : CreatureEffect.HYPNOSIS_ENDED;
        packetSender.Send(new CreatureEffectPlayed(creatureId, effect, 0, Optional.Of(remotePlayerIdentifier.RemotePlayer.PlayerContext.PlayerNitroxId)));
    }

    public void Apply(CreatureEffectPlayed packet)
    {
        if (simulationOwnership.HasAnyLockType(packet.CreatureId) || !NitroxEntity.TryGetObjectFrom(packet.CreatureId, out GameObject creatureObject))
        {
            return;
        }
        LiveMixin liveMixin = creatureObject.GetComponent<LiveMixin>();
        if (liveMixin && !liveMixin.IsAlive())
        {
            return;
        }

        applyingReceived = true;
        try
        {
            switch (packet.Effect)
            {
                case CreatureEffect.GAS_PODS_RELEASED:
                    ReleaseGas(creatureObject, packet.Seed);
                    break;
                case CreatureEffect.EMP_BLAST_CAST:
                    EMPAttack empAttack = creatureObject.GetComponentInChildren<EMPAttack>();
                    if (empAttack)
                    {
                        empAttack.CastEMPBlast();
                    }
                    break;
                case CreatureEffect.HYPNOSIS_STARTED:
                case CreatureEffect.HYPNOSIS_ENDED:
                    Hypnotize(creatureObject, packet);
                    break;
            }
        }
        finally
        {
            applyingReceived = false;
        }
    }

    private static void ReleaseGas(GameObject creatureObject, int seed)
    {
        if (!creatureObject.TryGetComponent(out GasoPod gasoPod))
        {
            return;
        }
        Random.State previous = Random.state;
        Random.InitState(seed);
        try
        {
            gasoPod.timeLastGasPodDrop = Time.time;
            gasoPod.DropGasPods();
        }
        finally
        {
            Random.state = previous;
        }
    }

    /// <summary>
    ///     On the hypnotized player's game, its copy's target is this player (its aim is sent with the rest): the game's
    ///     own start and stop of the hypnosis then pull this player and its view.
    /// </summary>
    private static void Hypnotize(GameObject creatureObject, CreatureEffectPlayed packet)
    {
        if (!packet.TargetId.HasValue || !Player.main || !Player.main.TryGetNitroxId(out NitroxId localPlayerId) || localPlayerId != packet.TargetId.Value ||
            !creatureObject.TryGetComponent(out Creature creature))
        {
            return;
        }
        PullCreatures pullCreatures = creatureObject.GetComponentInChildren<PullCreatures>();
        if (!pullCreatures)
        {
            return;
        }
        if (packet.Effect == CreatureEffect.HYPNOSIS_STARTED)
        {
            pullCreatures.lastTarget.SetTargetInternal(Player.main.gameObject);
            pullCreatures.StartPerform(creature, Time.time);
        }
        else
        {
            pullCreatures.StopPerform(creature, Time.time);
        }
    }

    private bool IsDecidedHere(GameObject creatureObject, out NitroxId creatureId)
    {
        if (!creatureObject.TryGetNitroxId(out creatureId))
        {
            creatureId = null;
            return true;
        }
        return simulationOwnership.HasAnyLockType(creatureId);
    }
}
