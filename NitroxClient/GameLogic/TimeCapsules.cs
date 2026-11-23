using System.Collections;
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
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;
using UWE;
using Object = UnityEngine.Object;

namespace NitroxClient.GameLogic;

/// <summary>
///     The time capsules: the one in the rocket's cockpit that the players fill together, and the ones of other players of
///     the game that are found in the world.
///     <para>
///     The game has a single PlayerTimeCapsule, a scene object outside the rocket. It is given the id of the rocket's
///     <see cref="PlayerTimeCapsuleEntity" />, which makes its container a container like a locker's and its text the
///     entity's metadata. Its picture stays with the player who chose it: it is a screenshot on that player's computer,
///     one for all rockets.
///     </para>
///     <para>
///     With several rockets the capsule shows one rocket's capsule at a time: the first one it is given, and from then on
///     the one of the rocket from whose cockpit it is opened or which is launched (<see cref="Show" />). Every rocket knows
///     its capsule entity through <see cref="RocketTimeCapsule" />.
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
    private readonly EntityMetadataManager entityMetadataManager;

    /// <summary>
    ///     The capsule of the rocket being launched, to be submitted once its content has arrived.
    /// </summary>
    private NitroxId? submitWhenShown;

    public TimeCapsules(IPacketSender packetSender, Entities entities, EntityMetadataManager entityMetadataManager)
    {
        this.packetSender = packetSender;
        this.entities = entities;
        this.entityMetadataManager = entityMetadataManager;
    }

    /// <summary>
    ///     Makes the time capsule of a rocket that this player builds. Nobody sends this player a spawn of it, so the
    ///     game's capsule takes its id here.
    /// </summary>
    public static PlayerTimeCapsuleEntity CreateRocketCapsule(GameObject rocketObject, NitroxId rocketId)
    {
        PlayerTimeCapsuleEntity capsuleEntity = new(new NitroxId(), rocketId, null);
        RocketTimeCapsule.Attach(rocketObject, capsuleEntity.Id);
        TakeCapsule(capsuleEntity.Id);
        return capsuleEntity;
    }

    /// <summary>
    ///     Tells a rocket which capsule entity is its own, also when the game's capsule shows another rocket's.
    /// </summary>
    public static void RegisterRocketCapsule(PlayerTimeCapsuleEntity capsuleEntity)
    {
        if (!NitroxEntity.TryGetObjectFrom(capsuleEntity.ParentId, out GameObject rocketObject))
        {
            Log.Error($"[{nameof(TimeCapsules)}] Could not find the rocket {capsuleEntity.ParentId} of the time capsule {capsuleEntity.Id}");
            return;
        }

        RocketTimeCapsule rocketCapsule = RocketTimeCapsule.Attach(rocketObject, capsuleEntity.Id);
        rocketCapsule.HasText = capsuleEntity.Metadata is PlayerTimeCapsuleMetadata { Text: { Length: > 0 } };
        rocketCapsule.HasItems = capsuleEntity.ChildEntities.Count > 0;
    }

    /// <summary>
    ///     Whether the game's capsule has to be given to another rocket's capsule to show it. With a single rocket it
    ///     always shows that rocket's.
    /// </summary>
    internal static bool MustSwap(NitroxId? shownId, NitroxId capsuleId)
    {
        return !capsuleId.Equals(shownId);
    }

    /// <summary>
    ///     The game's time capsule shows <paramref name="rocketCapsule" />'s capsule from now on. When it showed another
    ///     rocket's, that one is put away in this game only and the server is asked for the content
    ///     (<see cref="ShowContent" />).
    /// </summary>
    /// <returns>True when the capsule showed this rocket's capsule already.</returns>
    public bool Show(RocketTimeCapsule rocketCapsule)
    {
        PlayerTimeCapsule capsule = PlayerTimeCapsule.main;
        if (!capsule)
        {
            return false;
        }

        capsule.TryGetNitroxId(out NitroxId? shownId);
        if (!MustSwap(shownId, rocketCapsule.CapsuleId))
        {
            return true;
        }

        PutAway(capsule, shownId);
        NitroxEntity.SetNewId(capsule.gameObject, rocketCapsule.CapsuleId);
        packetSender.Send(new PlayerTimeCapsuleContentRequest(rocketCapsule.CapsuleId));
        return false;
    }

    /// <summary>
    ///     To be called when the capsule is opened from a rocket's cockpit.
    /// </summary>
    public void ShowCapsuleOfRocket(Component cockpitPart)
    {
        RocketTimeCapsule rocketCapsule = cockpitPart.GetComponentInParent<RocketTimeCapsule>();
        if (rocketCapsule)
        {
            Show(rocketCapsule);
        }
    }

    /// <summary>
    ///     Submits the capsule of the rocket being launched, which the game does at the launch. When the game's capsule
    ///     showed another rocket's, the submit waits for the launching rocket's content.
    /// </summary>
    public void SubmitCapsuleOfRocket(GameObject rocketObject)
    {
        RocketTimeCapsule rocketCapsule = rocketObject.GetComponent<RocketTimeCapsule>();
        if (!rocketCapsule || Show(rocketCapsule))
        {
            PlayerTimeCapsule.main.Submit(null);
            return;
        }
        submitWhenShown = rocketCapsule.CapsuleId;
    }

    /// <summary>
    ///     Shows the text and items the server sent for the capsule this game shows now.
    /// </summary>
    public void ShowContent(PlayerTimeCapsuleContent content)
    {
        PlayerTimeCapsule capsule = PlayerTimeCapsule.main;
        // The player may have opened yet another rocket's capsule since asking
        if (!capsule || !capsule.TryGetNitroxId(out NitroxId? shownId) || MustSwap(shownId, content.CapsuleId))
        {
            return;
        }

        // The server's answer is newer than any update for this capsule that arrived while it was put away
        Optional<IEntityMetadataProcessor> textProcessor = entityMetadataManager.FromMetaData(content.Metadata);
        if (textProcessor.HasValue)
        {
            textProcessor.Value.ProcessMetadata(capsule.gameObject, content.Metadata);
        }

        // An item can still be in this game where it was before it went into the capsule, e.g. in another player's inventory
        entities.CleanupExistingEntities(content.Items);
        CoroutineHost.StartCoroutine(ShowItems(content.CapsuleId, content.Items));
    }

    private IEnumerator ShowItems(NitroxId capsuleId, List<Entity> items)
    {
        // Objects destroyed by the cleanup are gone at the end of the frame
        yield return null;
        yield return entities.SpawnBatchAsync([.. items], true);

        PlayerTimeCapsule capsule = PlayerTimeCapsule.main;
        if (!capsule || !capsule.TryGetNitroxId(out NitroxId? shownId) || MustSwap(shownId, capsuleId))
        {
            yield break;
        }
        if (capsuleId.Equals(submitWhenShown))
        {
            submitWhenShown = null;
            capsule.Submit(null);
        }
    }

    /// <summary>
    ///     Removes what the game's capsule shows of another rocket's capsule, in this game only: on the server the items
    ///     stay that capsule's children and its text its metadata, and they come back when it is shown again.
    /// </summary>
    private void PutAway(PlayerTimeCapsule capsule, NitroxId? shownId)
    {
        if (shownId != null)
        {
            foreach (RocketTimeCapsule rocketCapsule in Object.FindObjectsOfType<RocketTimeCapsule>())
            {
                if (shownId.Equals(rocketCapsule.CapsuleId))
                {
                    rocketCapsule.HasText = capsule.hasText;
                    rocketCapsule.HasItems = capsule.hasItems;
                }
            }
        }

        foreach (InventoryItem item in new List<InventoryItem>(capsule.container))
        {
            Pickupable pickupable = item.item;
            capsule.container.RemoveItem(pickupable, true);
            if (pickupable.TryGetNitroxId(out NitroxId? itemId))
            {
                entities.RemoveEntity(itemId);
            }
            Entities.DestroyObject(pickupable.gameObject);
        }
        capsule.text = "";
        capsule.title = "";
    }

    /// <summary>
    ///     The time capsule preflight check of a rocket whose capsule is put away in this game: what the game's capsule
    ///     would say if it showed that rocket's. The picture is this player's own for every rocket.
    /// </summary>
    /// <returns>False when the game's own check applies: a single rocket, or the rocket whose capsule is shown.</returns>
    public bool TryGetPutAwayPreflight(RocketPreflightCheckManager preflightCheckManager, out bool complete)
    {
        complete = false;
        PlayerTimeCapsule capsule = PlayerTimeCapsule.main;
        RocketTimeCapsule rocketCapsule = preflightCheckManager.GetComponentInParent<RocketTimeCapsule>();
        if (!capsule || !rocketCapsule || preflightCheckManager.preflightChecks.Contains(PreflightCheck.TimeCapsule))
        {
            return false;
        }

        capsule.TryGetNitroxId(out NitroxId? shownId);
        if (!MustSwap(shownId, rocketCapsule.CapsuleId))
        {
            return false;
        }
        complete = IsReady(rocketCapsule.HasText, capsule.hasImage, rocketCapsule.HasItems);
        return true;
    }

    /// <summary>
    ///     As <see cref="PlayerTimeCapsule.IsValid" />.
    /// </summary>
    internal static bool IsReady(bool hasText, bool hasImage, bool hasItems)
    {
        return hasText && hasImage && hasItems;
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
    ///     The game has one time capsule for all rockets: a spawn gives it the first rocket capsule only; another rocket's
    ///     is shown when it is opened (<see cref="Show" />).
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
