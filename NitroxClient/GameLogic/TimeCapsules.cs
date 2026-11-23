using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The time capsule in the rocket's cockpit, which the players fill together.
///     <para>
///     The game has a single PlayerTimeCapsule, a scene object outside the rocket. It is given the id of the rocket's
///     <see cref="PlayerTimeCapsuleEntity" />, which makes its container a container like a locker's and its text the
///     entity's metadata. Its picture stays with the player who chose it: it is a screenshot on that player's computer.
///     </para>
/// </summary>
public class TimeCapsules
{
    private readonly Entities entities;

    public TimeCapsules(Entities entities)
    {
        this.entities = entities;
    }

    /// <summary>
    ///     Makes the time capsule of a rocket that this player builds. Nobody sends this player a spawn of it, so the
    ///     game's capsule takes its id here.
    /// </summary>
    public static PlayerTimeCapsuleEntity CreateRocketCapsule(NitroxId rocketId)
    {
        PlayerTimeCapsuleEntity capsuleEntity = new(new NitroxId(), rocketId, null);
        TakeCapsule(capsuleEntity.Id);
        return capsuleEntity;
    }

    /// <summary>
    ///     Gives the game's time capsule the id of a rocket's capsule entity.
    /// </summary>
    /// <returns>The capsule's object, or null when there is none or it is another rocket's already.</returns>
    public static GameObject? TakeCapsule(NitroxId capsuleId)
    {
        PlayerTimeCapsule capsule = PlayerTimeCapsule.main;
        if (!capsule)
        {
            Log.Error($"[{nameof(TimeCapsules)}] The game has no time capsule to give the id {capsuleId}");
            return null;
        }

        capsule.TryGetNitroxId(out NitroxId? boundId);
        if (!CanTakeCapsule(boundId, capsuleId))
        {
            Log.Warn($"[{nameof(TimeCapsules)}] The time capsule already belongs to the rocket capsule {boundId}; the capsule {capsuleId} of another rocket is not shown");
            return null;
        }

        NitroxEntity.SetNewId(capsule.gameObject, capsuleId);
        return capsule.gameObject;
    }

    /// <summary>
    ///     The game has one time capsule for all rockets: it stays with the first rocket capsule it is given.
    /// </summary>
    internal static bool CanTakeCapsule(NitroxId? boundId, NitroxId capsuleId)
    {
        return boundId == null || boundId.Equals(capsuleId);
    }

    /// <summary>
    ///     To be called before the game stores the text typed into the time capsule tab.
    /// </summary>
    public void BroadcastText(string text)
    {
        PlayerTimeCapsule capsule = PlayerTimeCapsule.main;
        // Opening the tab writes the stored text into its field again, which is no news to the others
        if (!capsule || capsule.text == text || !capsule.TryGetNitroxId(out NitroxId? capsuleId))
        {
            return;
        }

        // Stored a moment before the game stores it, so that the extractor reads the new text
        capsule.text = text;
        // Every key press changes the text
        entities.EntityMetadataChangedThrottled(capsule, capsuleId);
    }
}
