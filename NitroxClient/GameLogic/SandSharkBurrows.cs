using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Shows every player a Sand Shark going under the sand and bursting out of it. Only the simulating player's game runs
///     BurryInSand.Perform, which hides the shark: it holds the shark still (kinematic) where it lies, covers it with sand
///     and plays the burrow sound. The other games' copies follow its movement but stayed a shark lying on the sand, and
///     their own BurryInSand.Update kept them in the swimming pose. They now do what the simulating game did:<br />
///     - the sand starting to cover it is a moment (<see cref="SandSharkCoverStarted" /> packet), with the sound each game
///     plays once for itself<br />
///     - lying buried is the shark's state (<see cref="SandSharkMetadata" />), which the server keeps for players who load
///     the shark later and after a restart; applied, it shows the shark buried without the sound of going under
/// </summary>
public sealed class SandSharkBurrows(IPacketSender packetSender, SimulationOwnership simulationOwnership, SandSharkMetadataExtractor metadataExtractor)
{
    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly SandSharkMetadataExtractor metadataExtractor = metadataExtractor;

    /// <summary>
    ///     True while this game replays a burrow it received, through the same methods the simulating game's burrow is sent from.
    /// </summary>
    private bool applying;

    public void CoverStarted(BurryInSand burryInSand)
    {
        if (!applying && burryInSand.TryGetNitroxId(out NitroxId creatureId))
        {
            BroadcastCoverStarted(creatureId, burryInSand.burryPosition.ToDto(), burryInSand.floorAlign.ToDto());
        }
    }

    public void Buried(BurryInSand burryInSand) => BroadcastState(burryInSand);

    public void DugUp(BurryInSand burryInSand) => BroadcastState(burryInSand);

    public void ApplyCoverStarted(SandSharkCoverStarted coverStarted)
    {
        if (!ShouldApply(coverStarted.CreatureId) || !NitroxEntity.TryGetComponentFrom(coverStarted.CreatureId, out BurryInSand burryInSand) || IsDead(burryInSand))
        {
            return;
        }

        burryInSand.burryPosition = coverStarted.Position.ToUnity();
        burryInSand.floorAlign = coverStarted.Rotation.ToUnity();
        // Like BurryInSand.Perform on its last stretch: without it, BurryInSand.Update would dig the shark up at once
        SetKinematic(burryInSand, true);
        if (!burryInSand.coverInSandEffectStarted)
        {
            applying = true;
            try
            {
                burryInSand.TriggerSandCoverEffect();
            }
            finally
            {
                applying = false;
            }
        }
    }

    /// <summary>
    ///     Called by the metadata processor, when the shark spawns and when its simulating player's shark changes.
    /// </summary>
    public void ApplyState(BurryInSand burryInSand, bool buried, Vector3 burryPosition, Quaternion floorAlign)
    {
        if (IsDead(burryInSand))
        {
            return;
        }

        applying = true;
        try
        {
            if (buried)
            {
                // Set by BurryInSand.Start, which has not run yet on a shark that is being spawned
                if (burryInSand.animators == null)
                {
                    burryInSand.animators = burryInSand.GetComponentsInChildren<Animator>();
                }
                SetKinematic(burryInSand, true);
                burryInSand.burryPosition = burryPosition;
                burryInSand.floorAlign = floorAlign;
                burryInSand.transform.rotation = floorAlign;
                burryInSand.Burry();
            }
            else if (burryInSand.burried || burryInSand.coverInSandEffectStarted)
            {
                // BurryInSand.Update digs a shark up once it moves freely again; this copy then follows the shark's movement
                burryInSand.burried = false;
                burryInSand.coverInSandEffectStarted = false;
                SetKinematic(burryInSand, false);
            }
        }
        finally
        {
            applying = false;
        }
    }

    /// <summary>
    ///     Only the simulating player's game decides when the shark burrows.
    /// </summary>
    internal bool ShouldApply(NitroxId creatureId)
    {
        return !simulationOwnership.HasAnyLockType(creatureId);
    }

    internal void BroadcastCoverStarted(NitroxId creatureId, NitroxVector3 position, NitroxQuaternion rotation)
    {
        if (simulationOwnership.HasAnyLockType(creatureId))
        {
            packetSender.Send(new SandSharkCoverStarted(creatureId, position, rotation));
        }
    }

    internal void BroadcastState(NitroxId creatureId, SandSharkMetadata metadata)
    {
        if (simulationOwnership.HasAnyLockType(creatureId))
        {
            packetSender.Send(new EntityMetadataUpdate(creatureId, metadata));
        }
    }

    private void BroadcastState(BurryInSand burryInSand)
    {
        if (applying || !burryInSand.TryGetComponent(out SandShark sandShark) || !sandShark.TryGetNitroxId(out NitroxId creatureId))
        {
            return;
        }
        SandSharkMetadata metadata = metadataExtractor.Extract(sandShark);
        if (metadata != null)
        {
            BroadcastState(creatureId, metadata);
        }
    }

    private static void SetKinematic(BurryInSand burryInSand, bool kinematic)
    {
        if (burryInSand.TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.isKinematic = kinematic;
        }
    }

    /// <summary>
    ///     Sent before the simulating player learned that the shark died
    /// </summary>
    private static bool IsDead(Component creatureComponent)
    {
        LiveMixin liveMixin = creatureComponent.GetComponent<LiveMixin>();
        return liveMixin && !liveMixin.IsAlive();
    }
}
