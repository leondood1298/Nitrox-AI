using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Helpers;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.CinematicController;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Makes the cure balls (EnzymeBall) that a Sea Emperor baby drops one set for every player:<br />
///     - only the player who simulates a baby that is an entity (hatched in the prison, or appeared outside, see
///     <see cref="SeaEmperorBabies" />) makes its balls, and announces each ball as soon as it is made, so that the others get
///     the same ball in the same place, as do players who come later<br />
///     - a ball that cures a creature is used up for everybody<br />
///     - a ball cures a creature only in the game of the player who simulates the creature: the others leave the ball where it
///     is, so that the cure happens there and is not lost
/// </summary>
public class EnzymeBalls
{
    private readonly SimulationOwnership simulationOwnership;
    private readonly Items items;
    private readonly IPacketSender packetSender;

    public EnzymeBalls(SimulationOwnership simulationOwnership, Items items, IPacketSender packetSender)
    {
        this.simulationOwnership = simulationOwnership;
        this.items = items;
        this.packetSender = packetSender;
    }

    /// <summary>
    ///     Called before DropEnzymes.SpawnCureBall. The drop animation plays in every game, the ball is only made by the
    ///     baby's simulating player.
    /// </summary>
    public bool MaySpawn(DropEnzymes dropEnzymes)
    {
        return !TryGetSyncedBabyId(dropEnzymes, out NitroxId babyId) || simulationOwnership.HasAnyLockType(babyId);
    }

    /// <summary>
    ///     Called when DropEnzymes.SpawnCureBallAsync has made the ball and put it on the baby. The game lets a player click
    ///     the ball (the cure) while the baby still holds it, so the ball is an entity from now on: the others are given it at
    ///     once, and it follows the baby for them because this player broadcasts its position.
    /// </summary>
    public void Spawned(DropEnzymes dropEnzymes)
    {
        GameObject ball = dropEnzymes.spawnedCureBall;
        if (!ball || ball.TryGetNitroxId(out _) || !TryGetSyncedBabyId(dropEnzymes, out NitroxId babyId) || !simulationOwnership.HasAnyLockType(babyId))
        {
            return;
        }

        // Gives the ball its id and makes this player simulate its movement
        items.Dropped(ball, TechType.EnzymeCureBall);

        // The ball was made on the baby, so its cure cinematic may have been registered under the baby's id already.
        // Registering it under the ball as well lets this player see another player's cure on it.
        foreach (PlayerCinematicController controller in ball.GetComponentsInChildren<PlayerCinematicController>(true))
        {
            ball.EnsureComponent<MultiplayerCinematicReference>().AddController(controller);
        }
    }

    /// <summary>
    ///     Called before EnzymeBall.OnTriggerEnter.
    /// </summary>
    /// <param name="curesCreature">True if the ball is going to cure the creature and be used up.</param>
    /// <returns>False if the touch must be ignored here.</returns>
    public bool MayCure(Collider collider, out bool curesCreature)
    {
        curesCreature = false;
        Creature creature = UWE.Utils.GetComponentInHierarchy<Creature>(collider.gameObject);
        if (!creature)
        {
            return true;
        }
        if (creature.TryGetNitroxId(out NitroxId creatureId) && !simulationOwnership.HasAnyLockType(creatureId))
        {
            // Only the creature's simulating player may change its infection: curing it here would fail and still use up
            // the ball. The same ball touches the creature in that player's game.
            return false;
        }
        InfectedMixin infectedMixin = UWE.Utils.GetComponentInHierarchy<InfectedMixin>(collider.gameObject);
        curesCreature = infectedMixin && infectedMixin.IsInfected();
        return true;
    }

    /// <summary>
    ///     Called after EnzymeBall.OnTriggerEnter, which destroys the ball when it cured a creature.
    /// </summary>
    public void OnTouched(EnzymeBall ball, bool curedCreature)
    {
        if (curedCreature && ball.TryGetNitroxId(out NitroxId ballId))
        {
            packetSender.Send(new EntityDestroyed(ballId));
        }
    }

    private static bool TryGetSyncedBabyId(DropEnzymes dropEnzymes, out NitroxId babyId)
    {
        babyId = null;
        SeaEmperorBaby baby = dropEnzymes.GetComponentInParent<SeaEmperorBaby>();
        return baby && baby.TryGetNitroxId(out babyId);
    }
}
