using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The time capsules: the one in the rocket's cockpit that the players fill together, and the ones of other players of
///     the game that are found in the world.
///     <para>
///     The game has a single PlayerTimeCapsule, a scene object outside the rocket. It is given the id of the rocket's
///     <see cref="PlayerTimeCapsuleEntity" />, which makes its container a container like a locker's and its text the
///     entity's metadata. Its picture stays with the player who chose it: it is a screenshot on that player's computer.
///     </para>
/// </summary>
public class TimeCapsules
{
    private readonly IPacketSender packetSender;
    private readonly Entities entities;

    public TimeCapsules(IPacketSender packetSender, Entities entities)
    {
        this.packetSender = packetSender;
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

    /// <summary>
    ///     To be called before a found time capsule adds its encyclopedia entry. The game gives its items to the player
    ///     who collects it, and the capsule entity is destroyed when it is opened (TimeCapsule_Open_Patch).
    /// </summary>
    public void BroadcastCollected(TimeCapsule timeCapsule)
    {
        string contentId = timeCapsule.id;
        if (string.IsNullOrEmpty(contentId))
        {
            return;
        }

        // Only this player's game fetched the content, from the online service
        TimeCapsuleContentProvider.GetData(contentId, out string title, out string text, out string imageUrl);
        packetSender.Send(new TimeCapsuleCollected(new CollectedTimeCapsule(contentId, title, text, imageUrl)));
    }

    /// <summary>
    ///     Adds the encyclopedia entry of a time capsule another player collected, with the content that player's game
    ///     fetched.
    /// </summary>
    public void AddCollected(CollectedTimeCapsule collected, bool verbose)
    {
        if (string.IsNullOrEmpty(collected.Id))
        {
            return;
        }

        // Content this game fetched itself is kept: it also lists the items
        if (!TimeCapsuleContentProvider.GetData(collected.Id, out _, out _, out _))
        {
            TimeCapsuleContentProvider.Set(collected.Id, new TimeCapsuleContent
            {
                title = collected.Title,
                text = collected.Text,
                imageUrl = collected.ImageUrl,
                items = [],
                isActive = true
            });
        }

        // The entry is made from the content, which only this packet and the initial sync bring; the capsule's id is no
        // entry key of the game's
        using (PacketSuppressor<PDAEncyclopediaEntryAdd>.Suppress())
        {
            PDAEncyclopedia.AddTimeCapsule(collected.Id, verbose);
        }
    }

    /// <summary>
    ///     For a joining player: the content and the encyclopedia entries of the collected time capsules, which the game
    ///     would read from its own save.
    /// </summary>
    public void RestoreCollected(IEnumerable<CollectedTimeCapsule> collectedCapsules)
    {
        foreach (CollectedTimeCapsule collected in collectedCapsules)
        {
            AddCollected(collected, false);
        }
    }
}
