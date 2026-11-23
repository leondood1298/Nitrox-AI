using System.Collections;
using System.Collections.Generic;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic
{
    /// <summary>
    /// Handles all of the <see cref="Fire"/>s in the game. Currently, the only known Fire spawning is in <see cref="SubFire.CreateFire(SubFire.RoomFire)"/>. The
    /// fires in the Aurora come loaded with the map and do not grow in size. If we want to create a Fire spawning mechanic outside of Cyclops fires, it should be
    /// added to <see cref="Fires.Create(CyclopsFireData)"/>. Fire dousing goes by Id and does not need to be
    /// modified
    /// </summary>
    public class Fires
    {
        private readonly IPacketSender packetSender;

        /// <summary>
        /// A douse amount larger than any fire's health. Sent when a fire went out locally, so it goes out for everyone
        /// no matter how far their copy had been doused.
        /// </summary>
        public const float EXTINGUISH_DOUSE_AMOUNT = 10000f;

        /// <summary>
        /// How long to wait for the game to finish spawning a fire it was asked to create.
        /// </summary>
        private const float NEW_FIRE_WAIT_SECONDS = 5f;

        /// <summary>
        /// Used to reduce the <see cref="FireDoused"/> packet spam as fires are being doused.
        /// </summary>
        private readonly FireDouseLedger douseLedger = new();

        public Fires(IPacketSender packetSender)
        {
            this.packetSender = packetSender;
        }

        /// <summary>
        /// Triggered when <see cref="SubFire.CreateFire(SubFire.RoomFire)"/> has run for the player simulating the Cyclops.
        /// The game spawns the fire object through a spawn request that completes later, so the new fire is looked for
        /// over the next frames instead of right away.
        /// </summary>
        public void AnnounceNewFire(SubFire subFire, SubFire.RoomFire room)
        {
            UWE.CoroutineHost.StartCoroutine(AnnounceWhenSpawned(subFire, room));
        }

        private IEnumerator AnnounceWhenSpawned(SubFire subFire, SubFire.RoomFire room)
        {
            float deadline = Time.realtimeSinceStartup + NEW_FIRE_WAIT_SECONDS;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!subFire)
                {
                    yield break;
                }
                for (int nodeIndex = 0; nodeIndex < room.spawnNodes.Length; nodeIndex++)
                {
                    Transform node = room.spawnNodes[nodeIndex];
                    if (!node || node.childCount == 0)
                    {
                        continue;
                    }
                    Fire fire = node.GetComponentInChildren<Fire>();
                    // A fire with an id has been announced already, by this player or by the one who created it.
                    if (!fire || fire.TryGetNitroxId(out _))
                    {
                        continue;
                    }
                    OnCreate(fire, room, nodeIndex);
                    yield break;
                }
                yield return null;
            }
            Log.Warn("[Fires] A Cyclops fire was requested but did not appear, so it was not announced to other players");
        }

        /// <summary>
        /// Gives a locally created fire its id and tells the other players. To create a fire announced by someone else,
        /// call <see cref="Create(CyclopsFireData)"/>
        /// </summary>
        public void OnCreate(Fire fire, SubFire.RoomFire room, int nodeIndex)
        {
            if (!fire.fireSubRoot || !fire.fireSubRoot.TryGetIdOrWarn(out NitroxId subRootId))
            {
                return;
            }
            // Nothing else assigns an id to a fire the game spawned here. Without one it can be neither created nor doused
            // for the other players.
            NitroxId fireId = NitroxEntity.GetIdOrGenerateNew(fire.gameObject);

            CyclopsFireCreated packet = new CyclopsFireCreated(fireId, subRootId, room.roomLinks.room, nodeIndex);
            packetSender.Send(packet);
        }

        /// <summary>
        /// Triggered when <see cref="Fire.Douse(float)"/> is executed. To Douse a fire manually, retrieve the <see cref="Fire"/> call the Douse method
        /// </summary>
        public void OnDouse(Fire fire, float douseAmount)
        {
            if (!fire.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            // Dousing applied from another player's packet also lands here. Counting it would send it back out later
            // as if this player had done it.
            if (PacketSuppressor<FireDoused>.IsSuppressed)
            {
                return;
            }

            float amountToSend = douseLedger.Record(fireId, douseAmount, douseAmount >= EXTINGUISH_DOUSE_AMOUNT);
            if (amountToSend > 0f)
            {
                packetSender.Send(new FireDoused(fireId, amountToSend));
            }
        }

        /// <summary>
        /// Create a new <see cref="Fire"/>. Majority of code copied from <see cref="SubFire.CreateFire(SubFire.RoomFire)"/>. Currently does not support Fires created outside of a Cyclops
        /// </summary>
        public void Create(CyclopsFireData fireData)
        {
            SubFire subFire = NitroxEntity.RequireObjectFrom(fireData.CyclopsId).GetComponent<SubRoot>().damageManager.subFire;
            Dictionary<CyclopsRooms, SubFire.RoomFire> roomFiresDict = subFire.roomFires;
            // Copied from SubFire_CreateFire_Patch, which copies from SubFire.CreateFire()
            Transform transform2 = roomFiresDict[fireData.Room].spawnNodes[fireData.NodeIndex];

            // If a fire already exists at the node, replace the old Id with the new one
            if (transform2.childCount > 0)
            {
                Fire existingFire = transform2.GetComponentInChildren<Fire>();

                // Giving the fire the Cyclops' id here, as this once did, takes that id away from the Cyclops itself.
                if (existingFire && (!existingFire.TryGetNitroxId(out NitroxId existingFireId) || existingFireId != fireData.FireId))
                {
                    Log.Warn($"[Fires.Create] A fire already exists at node index {fireData.NodeIndex}; giving it id {fireData.FireId}");

                    NitroxEntity.SetNewId(existingFire.gameObject, fireData.FireId);
                }

                return;
            }

            List<Transform> availableNodes = subFire.availableNodes;
            availableNodes.Clear();
            foreach (Transform transform in roomFiresDict[fireData.Room].spawnNodes)
            {
                if (transform.childCount == 0)
                {
                    availableNodes.Add(transform);
                }
            }

            roomFiresDict[fireData.Room].fireValue++;
            // SubFire.CreateFire looks the spawner up by its base type.
            PrefabSpawnBase component = transform2.GetComponent<PrefabSpawnBase>();
            if (!component)
            {
                Log.Error(
                    $"[{nameof(CyclopsFireCreatedProcessor)} Cannot create new Cyclops fire! PrefabSpawn component could not be found in fire node! Fire Id: {fireData.FireId} SubRoot Id: {fireData.CyclopsId} Room: {fireData.Room} NodeIndex: {fireData.NodeIndex}]");
                return;
            }

            component.SpawnManual(delegate(GameObject fireGO)
            {
                Fire componentInChildren = fireGO.GetComponentInChildren<Fire>();
                if (componentInChildren)
                {
                    componentInChildren.fireSubRoot = subFire.subRoot;
                    NitroxEntity.SetNewId(componentInChildren.gameObject, fireData.FireId);
                }
            });

            subFire.roomFires = roomFiresDict;
            subFire.availableNodes = availableNodes;
        }
    }
}
