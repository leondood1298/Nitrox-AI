using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Remembers where the next new items come from, so that their pickup or drop can name its source (see
///     <see cref="ItemSource" />): the server then gives an item that two games took from the same source at the same moment
///     to one of them. The game makes such items a few frames after the click that takes them, so the click announces
///     them here and <see cref="Items" /> collects the announcement when the item arrives.
/// </summary>
public class ItemSources
{
    /// <summary>
    ///     How long an announced item is waited for, in seconds. Making an item takes a few frames.
    /// </summary>
    public const float EXPECTATION_LIFETIME = 10f;

    /// <summary>
    ///     Health one hit of a drill arm takes from a deposit's chunk (Drillable.OnDrill).
    /// </summary>
    private const float DRILL_HIT_DAMAGE = 5f;

    private readonly Func<float> clock;
    private readonly List<Expectation> expectations = [];

    public ItemSources() : this(() => Time.realtimeSinceStartup)
    {
    }

    internal ItemSources(Func<float> clock)
    {
        this.clock = clock;
    }

    /// <summary>
    ///     The next <paramref name="count" /> new items of <paramref name="techType" /> come from
    ///     <paramref name="sourceId" />. With more than one, each gets its own token: the n-th item is the same item in
    ///     every game.
    /// </summary>
    public void Expect(TechType techType, NitroxId sourceId, string token, int count = 1)
    {
        Add(new Expectation(techType, ItemSourceUse.PICKUP, sourceId, token, count, clock()));
    }

    /// <summary>
    ///     The next <paramref name="count" /> new items of any kind that are used as <paramref name="use" /> come from
    ///     <paramref name="sourceId" /> (the resources of a broken outcrop, the items of a time capsule). They all carry
    ///     <paramref name="token" />: the first game to claim the source gets all its items.
    /// </summary>
    public void ExpectAny(ItemSourceUse use, NitroxId sourceId, string token, int count)
    {
        Add(new Expectation(TechType.None, use, sourceId, token, count, clock()));
    }

    /// <summary>
    ///     Takes the source of a new item that is now picked up or dropped, if one was announced.
    /// </summary>
    public ItemSource? Take(TechType techType, ItemSourceUse use)
    {
        float now = clock();
        expectations.RemoveAll(expectation => expectation.Remaining <= 0 || now - expectation.Time > EXPECTATION_LIFETIME);

        // An announcement of the item's own kind comes before one of any kind
        Expectation? match = expectations.Find(expectation => expectation.TechType == techType && expectation.Use == use) ??
                             expectations.Find(expectation => expectation.TechType == TechType.None && expectation.Use == use);
        if (match == null)
        {
            return null;
        }

        // Items of any kind differ between games (an outcrop drops what each game's dice chose): they all share the token,
        // so the first game to claim the source gets all of its own and none of the others'.
        bool shared = match.Count == 1 || match.TechType == TechType.None;
        string token = shared ? match.Token : $"{match.Token}:{match.Count - match.Remaining}";
        match.Remaining--;
        return new ItemSource(match.SourceId, token);
    }

    /// <summary>
    ///     Called before a player clicks something that is picked (a fruit, a seed cluster, a sample). Its item is made
    ///     anew by every game that picks it.
    /// </summary>
    public void Picking(PickPrefab pickPrefab)
    {
        if (!pickPrefab.AllowedToPickUp())
        {
            return;
        }
        if (pickPrefab.TryGetReference(out FruitPlant fruitPlant) && fruitPlant.fruits != null)
        {
            int index = Array.IndexOf(fruitPlant.fruits, pickPrefab);
            if (fruitPlant.TryGetNitroxId(out NitroxId plantId) ||
                (fruitPlant.TryGetComponent(out GrownPlant grownPlant) && grownPlant.seed && grownPlant.seed.TryGetNitroxId(out plantId)))
            {
                Expect(pickPrefab.pickTech, plantId, $"fruit{index}");
            }
            return;
        }
        if (pickPrefab.TryGetNitroxId(out NitroxId pickId))
        {
            Expect(pickPrefab.pickTech, pickId, "pick");
        }
    }

    /// <summary>
    ///     Called before an outcrop breaks: the resources it drops are made anew by every game that breaks it.
    /// </summary>
    public void Breaking(BreakableResource breakable)
    {
        if (!breakable.broken && breakable.TryGetNitroxId(out NitroxId id))
        {
            // Every chance can drop one resource, and a default one is dropped when no chance did
            ExpectAny(ItemSourceUse.DROP, id, "break", breakable.numChances + 1);
        }
    }

    /// <summary>
    ///     Called before a drill arm hits a deposit (Drillable.OnDrill): when the hit breaks a chunk, the resources that
    ///     chunk gives are made anew by every game that breaks it.
    /// </summary>
    public void Drilling(Drillable drillable, Vector3 position)
    {
        if (drillable.resources.Length == 0 || !drillable.TryGetNitroxId(out NitroxId id))
        {
            return;
        }
        // The chunk the game picks for this hit
        int chunk = drillable.FindClosestMesh(position, out Vector3 _);
        if (HitBreaksChunk(drillable.health[chunk]))
        {
            ExpectAny(ItemSourceUse.DROP, id, $"chunk{chunk}", drillable.maxResourcesToSpawn);
        }
    }

    /// <summary>
    ///     Whether a chunk of a deposit with <paramref name="health" /> breaks when a drill arm hits it once more.
    /// </summary>
    internal static bool HitBreaksChunk(float health)
    {
        return health > 0f && health <= DRILL_HIT_DAMAGE;
    }

    private void Add(Expectation expectation)
    {
        if (expectation.Count <= 0)
        {
            return;
        }
        // A second click on the same source before its item arrived announces the same items again
        expectations.RemoveAll(other => other.SourceId == expectation.SourceId && other.Token == expectation.Token && other.TechType == expectation.TechType);
        expectations.Add(expectation);
    }

    private sealed class Expectation(TechType techType, ItemSourceUse use, NitroxId sourceId, string token, int count, float time)
    {
        public TechType TechType { get; } = techType;
        public ItemSourceUse Use { get; } = use;
        public NitroxId SourceId { get; } = sourceId;
        public string Token { get; } = token;
        public int Count { get; } = count;
        public float Time { get; } = time;
        public int Remaining { get; set; } = count;
    }
}

public enum ItemSourceUse
{
    PICKUP,
    DROP
}
