using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Extensions;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Reads and applies which hull leaks (<see cref="CyclopsDamagePoint" />) and fires are active on a Cyclops. The
///     player simulating the Cyclops decides both; everyone else is made to match, live through <see cref="CyclopsDamage" />
///     and when joining through the Cyclops' saved metadata.
/// </summary>
public sealed class CyclopsDamageSync(Fires fires, SimulationOwnership simulationOwnership)
{
    /// <summary>
    ///     How long to wait for a freshly spawned Cyclops to run its Start methods.
    /// </summary>
    private const float READY_WAIT_SECONDS = 15f;

    /// <summary>
    ///     Index of every active leak in <see cref="CyclopsExternalDamageManager.damagePoints" />, in ascending order.
    /// </summary>
    public static int[] GetActiveDamagePoints(SubRoot subRoot)
    {
        List<int> active = [];
        CyclopsDamagePoint[] damagePoints = subRoot.damageManager.damagePoints;
        for (int i = 0; i < damagePoints.Length; i++)
        {
            if (damagePoints[i].gameObject.activeSelf)
            {
                active.Add(i);
            }
        }
        return active.ToArray();
    }

    /// <summary>
    ///     Every burning fire on the Cyclops. <see cref="SubFire.RoomFire.spawnNodes" /> is a fixed list of the places a
    ///     fire can be, so a fire is identified by its room and node index.
    /// </summary>
    public static CyclopsFireData[] GetActiveRoomFires(SubFire subFire)
    {
        List<CyclopsFireData> active = [];
        if (!subFire || !subFire.subRoot.TryGetNitroxId(out NitroxId subRootId))
        {
            return active.ToArray();
        }

        foreach (KeyValuePair<CyclopsRooms, SubFire.RoomFire> roomFire in subFire.roomFires)
        {
            for (int i = 0; i < roomFire.Value.spawnNodes.Length; i++)
            {
                Transform node = roomFire.Value.spawnNodes[i];
                if (node.childCount == 0)
                {
                    continue;
                }
                // A fire that went out is still a child of its node until the game removes the object. A fire without
                // an id has not been announced yet and cannot be referred to by other players.
                Fire fire = node.GetComponentInChildren<Fire>();
                if (!fire || fire.IsExtinguished() || !fire.TryGetNitroxId(out NitroxId fireId))
                {
                    continue;
                }
                active.Add(new CyclopsFireData(fireId, subRootId, roomFire.Key, i));
            }
        }
        return active.ToArray();
    }

    /// <summary>
    ///     Applies saved leaks and fires to a Cyclops that may only just have spawned. The game fills in its leak and
    ///     fire tables in <c>CyclopsExternalDamageManager.Start</c> and <c>SubFire.Start</c>, which also reset them, so
    ///     this waits until both have run. Null means that state is unknown (older saves) and is left alone.
    /// </summary>
    public void ApplyWhenReady(GameObject cyclops, int[] damagePointIndexes, CyclopsFireData[] roomFires)
    {
        if (damagePointIndexes == null && roomFires == null)
        {
            return;
        }
        if (IsReady(cyclops))
        {
            Apply(cyclops, damagePointIndexes, roomFires);
            return;
        }
        UWE.CoroutineHost.StartCoroutine(ApplyOnceReady(cyclops, damagePointIndexes, roomFires));
    }

    private IEnumerator ApplyOnceReady(GameObject cyclops, int[] damagePointIndexes, CyclopsFireData[] roomFires)
    {
        float deadline = Time.realtimeSinceStartup + READY_WAIT_SECONDS;
        while (cyclops && !IsReady(cyclops))
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Log.Warn($"[{nameof(CyclopsDamageSync)}] Cyclops {cyclops.name} did not finish initialising; its saved leaks and fires were not applied");
                yield break;
            }
            yield return null;
        }
        if (cyclops)
        {
            Apply(cyclops, damagePointIndexes, roomFires);
        }
    }

    private static bool IsReady(GameObject cyclops)
    {
        // damageRecievers is assigned and roomFires is filled in the two Start methods.
        return cyclops.TryGetComponent(out SubRoot subRoot) && subRoot.damageManager && subRoot.damageManager.damageRecievers != null &&
               cyclops.TryGetComponent(out SubFire subFire) && subFire.roomFires.Count > 0;
    }

    private void Apply(GameObject cyclops, int[] damagePointIndexes, CyclopsFireData[] roomFires)
    {
        SubRoot subRoot = cyclops.GetComponent<SubRoot>();
        if (damagePointIndexes != null)
        {
            using (PacketSuppressor<CyclopsDamagePointRepaired>.Suppress())
            {
                SetActiveDamagePoints(subRoot, damagePointIndexes);
            }
        }
        if (roomFires != null)
        {
            using (PacketSuppressor<FireDoused>.Suppress())
            {
                SetActiveRoomFires(subRoot, roomFires);
            }
        }
    }

    /// <summary>
    ///     Add/remove <see cref="CyclopsDamagePoint" />s until they match <paramref name="damagePointIndexes" />. Can
    ///     trigger <see cref="CyclopsDamagePointRepaired" /> packets.
    /// </summary>
    public void SetActiveDamagePoints(SubRoot cyclops, int[] damagePointIndexes)
    {
        CyclopsExternalDamageManager damageManager = cyclops.gameObject.RequireComponentInChildren<CyclopsExternalDamageManager>();
        List<CyclopsDamagePoint> unusedDamagePoints = damageManager.unusedDamagePoints;
        HashSet<int> wanted = [..damagePointIndexes ?? []];
        // Repairing a leak gives hull health back. For the player simulating the Cyclops that would be real healing,
        // so there a leak that should not be active is left for the simulation to deal with.
        bool mayRepair = !cyclops.TryGetNitroxId(out NitroxId cyclopsId) || !simulationOwnership.HasAnyLockType(cyclopsId);

        // damagePoints is a fixed list, nothing is ever added to or removed from it, so indexes identify leaks.
        for (int i = 0; i < damageManager.damagePoints.Length; i++)
        {
            CyclopsDamagePoint damagePoint = damageManager.damagePoints[i];
            bool isActive = damagePoint.gameObject.activeSelf;
            if (wanted.Contains(i) && !isActive)
            {
                // Copied from CyclopsExternalDamageManager.CreatePoint(), except without the random index pick.
                damagePoint.gameObject.SetActive(true);
                damagePoint.RestoreHealth();
                GameObject prefabGo = damageManager.fxPrefabs[Random.Range(0, damageManager.fxPrefabs.Length)];
                damagePoint.SpawnFx(prefabGo);
                unusedDamagePoints.Remove(damagePoint);
            }
            else if (!wanted.Contains(i) && isActive && mayRepair)
            {
                // Active here but not for the simulating player: it has been repaired. The max health of a leak is 1.
                damagePoint.liveMixin.AddHealth(999);
            }
        }

        int outOfRange = wanted.Count(index => index < 0 || index >= damageManager.damagePoints.Length);
        if (outOfRange > 0)
        {
            Log.Error($"[{nameof(CyclopsDamageSync)}] {outOfRange} leak index(es) do not exist on this Cyclops");
        }

        // unusedDamagePoints is checked against damagePoints to decide if there are enough leaks. Leaving it stale
        // makes random leaks appear.
        damageManager.unusedDamagePoints = unusedDamagePoints;
        // Visual only: the water coming through the window and hull depending on missing health.
        damageManager.ToggleLeakPointsBasedOnDamage();
    }

    /// <summary>
    ///     Add/remove fires until they match <paramref name="roomFires" />. Can trigger <see cref="FireDoused" /> packets.
    /// </summary>
    public void SetActiveRoomFires(SubRoot subRoot, CyclopsFireData[] roomFires)
    {
        SubFire subFire = subRoot.gameObject.RequireComponent<SubFire>();
        if (!subRoot.TryGetIdOrWarn(out NitroxId subRootId))
        {
            return;
        }
        roomFires ??= [];

        foreach (KeyValuePair<CyclopsRooms, SubFire.RoomFire> room in subFire.roomFires)
        {
            for (int nodeIndex = 0; nodeIndex < room.Value.spawnNodes.Length; nodeIndex++)
            {
                Transform node = room.Value.spawnNodes[nodeIndex];
                CyclopsFireData wanted = roomFires.FirstOrDefault(fire => fire.Room == room.Key && fire.NodeIndex == nodeIndex);
                if (wanted == null)
                {
                    Fire leftover = node.childCount > 0 ? node.GetComponentInChildren<Fire>() : null;
                    if (leftover)
                    {
                        leftover.Douse(Fires.EXTINGUISH_DOUSE_AMOUNT);
                    }
                }
                else if (node.childCount < 1)
                {
                    fires.Create(new CyclopsFireData(wanted.FireId, subRootId, wanted.Room, wanted.NodeIndex));
                }
            }
        }
    }
}
