using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Extensions;
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
///     <para>
///     Whether a capsule lies at a spot in the world and what it holds is what the online service answers the first game
///     that asks (<see cref="TimeCapsuleMetadata" />); the other games show that answer instead of asking.
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
    ///     To be called instead of <see cref="TimeCapsule.Spawn" /> when the online service, asked by this game, placed a
    ///     capsule at a spot. The answer is offered to the server, which keeps the first one for everybody.
    /// </summary>
    public void OnServiceSpawned(TimeCapsule capsule, string instanceId, string contentId)
    {
        // Another game's answer arrived first and is shown already
        if (capsule.spawned)
        {
            return;
        }

        capsule.Spawn(instanceId, contentId);
        if (capsule.TryGetNitroxId(out NitroxId? capsuleId))
        {
            entities.BroadcastMetadataUpdate(capsuleId, CreateMetadata(instanceId, contentId));
        }
    }

    /// <summary>
    ///     To be called instead of <see cref="TimeCapsule.DoNotSpawn" /> when this game's online service placed no capsule at
    ///     a spot.
    /// </summary>
    /// <param name="serviceAnswered">
    ///     False when the service could not be reached: that tells nothing about the spot, so nothing is offered.
    /// </param>
    public void OnServiceDidNotSpawn(TimeCapsule capsule, bool serviceAnswered)
    {
        // Another game's answer arrived first and is shown already
        if (capsule.spawned)
        {
            return;
        }

        if (serviceAnswered && capsule.TryGetNitroxId(out NitroxId? capsuleId))
        {
            entities.BroadcastMetadataUpdate(capsuleId, TimeCapsuleMetadata.Empty());
        }
        capsule.DoNotSpawn();
    }

    /// <summary>
    ///     Shows the answer that is kept for a capsule in the world, instead of what this game's online service answers.
    /// </summary>
    public static void ApplyKeptAnswer(TimeCapsule capsule, TimeCapsuleMetadata metadata)
    {
        // The game asks the service from TimeCapsule.Start(); a capsule with a kept answer needs no asking
        PlayerTimeCapsule playerTimeCapsule = PlayerTimeCapsule.main;
        if (playerTimeCapsule)
        {
            playerTimeCapsule.UnregisterSpawn(capsule);
        }

        if (!metadata.HasCapsule)
        {
            capsule.DoNotSpawn();
            return;
        }

        // With the items: they are given to the player who opens the capsule (TimeCapsule.Collect)
        TimeCapsuleContentProvider.Set(metadata.ContentId, new TimeCapsuleContent
        {
            title = metadata.Title,
            text = metadata.Text,
            imageUrl = metadata.ImageUrl,
            items = metadata.Items.Select(ToGameItem).ToList(),
            isActive = true
        });
        capsule.Spawn(metadata.InstanceId, metadata.ContentId);
    }

    private static TimeCapsuleMetadata CreateMetadata(string instanceId, string contentId)
    {
        TimeCapsuleContentProvider.GetData(contentId, out string title, out string text, out string imageUrl);
        List<TimeCapsuleItem> items = TimeCapsuleContentProvider.GetItems(contentId) ?? [];
        return new TimeCapsuleMetadata(true, instanceId, contentId, title ?? "", text ?? "", imageUrl ?? "", items.Select(ToMetadataItem).ToList());
    }

    private static TimeCapsuleMetadata.Item ToMetadataItem(TimeCapsuleItem item)
    {
        return new TimeCapsuleMetadata.Item(item.techType.ToDto(), item.hasBattery, item.batteryType.ToDto(), item.batteryCharge);
    }

    private static TimeCapsuleItem ToGameItem(TimeCapsuleMetadata.Item item)
    {
        return new TimeCapsuleItem
        {
            techType = item.TechType.ToUnity(),
            hasBattery = item.HasBattery,
            batteryType = item.BatteryType.ToUnity(),
            batteryCharge = item.BatteryCharge
        };
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

        // The collector's game already reported the entry's key; a game that lacks the content cannot make the entry from
        // that key, which is why the content comes with this packet and the initial sync
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
