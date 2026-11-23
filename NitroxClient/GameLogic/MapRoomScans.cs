using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The scans of the Scanner Rooms this client has loaded: what a room scans for, what it has found, and which
///     types it offers to scan for. The server holds the state everybody sees. The player who simulates a room runs
///     its scan in their own game and reports what it finds; everybody else shows what the server sends. The scan's
///     power is drawn from the base in that player's game alone.
/// </summary>
public sealed partial class MapRoomScans
{
    /// <summary>
    ///     The shortest time between two reports of how far a room's scan has got. A guess, not measured.
    /// </summary>
    private const float PROGRESS_THROTTLE_SECONDS = 0.2f;

    private readonly IPacketSender packetSender;
    private readonly ThrottledPacketSender throttledPacketSender;
    private readonly SimulationOwnership simulationOwnership;
    private readonly BasePowerConsumptionForwarder powerConsumptionForwarder;
    private readonly Func<float> secondsSinceStartup;
    private readonly Func<bool> worldLoaded;

    /// <summary>
    ///     What this client knows about a room's scan that the game does not keep on the room itself.
    /// </summary>
    private readonly Dictionary<NitroxId, RoomState> statesByRoomId = [];

    public MapRoomScans(IPacketSender packetSender, ThrottledPacketSender throttledPacketSender, SimulationOwnership simulationOwnership,
        BasePowerConsumptionForwarder powerConsumptionForwarder)
        : this(packetSender, throttledPacketSender, simulationOwnership, powerConsumptionForwarder, static () => Time.realtimeSinceStartup,
            static () => Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted && !BasePowerSources.IsRebuildingBases())
    {
    }

    /// <param name="worldLoaded">
    ///     Whether this client's game has the world as the server sent it: false while it joins and while it builds its
    ///     bases anew.
    /// </param>
    internal MapRoomScans(IPacketSender packetSender, ThrottledPacketSender throttledPacketSender, SimulationOwnership simulationOwnership,
        BasePowerConsumptionForwarder powerConsumptionForwarder, Func<float> secondsSinceStartup, Func<bool> worldLoaded)
    {
        this.packetSender = packetSender;
        this.throttledPacketSender = throttledPacketSender;
        this.simulationOwnership = simulationOwnership;
        this.powerConsumptionForwarder = powerConsumptionForwarder;
        this.secondsSinceStartup = secondsSinceStartup;
        this.worldLoaded = worldLoaded;
    }

    /// <summary>
    ///     Called before the game updates a room's scan, which it does every frame and in every player's game. A room
    ///     that has an id gets its own update here (see <see cref="Update(IRoom)" />). The game also drains the room's
    ///     power in its update, whenever a timer has run out. Only the player who simulates the room may: everybody
    ///     else's timer is kept from running out, and the simulating player's drain is reported to whoever simulates
    ///     the power sources (see <see cref="BasePowerConsumptionForwarder" />).
    /// </summary>
    /// <returns>Whether this client simulates the room, for <see cref="ScanUpdated" />.</returns>
    public bool BeforeScanUpdate(MapRoomFunctionality mapRoom)
    {
        bool hasEntityId = mapRoom.TryGetNitroxId(out NitroxId roomId);
        bool hasOwnership = hasEntityId && simulationOwnership.HasAnyLockType(roomId);
        if (hasEntityId && IsRoomOf(roomId, mapRoom))
        {
            Update(GetRoom(mapRoom, roomId));
        }
        if (ShouldSuppressPowerDrain(hasEntityId, hasOwnership))
        {
            mapRoom.timeLastPowerDrain = Time.time;
        }
        // A room without an id drains on every client, which must not be reported once per player.
        if (hasOwnership)
        {
            powerConsumptionForwarder.EnterConsumer();
        }
        return hasOwnership;
    }

    /// <summary>
    ///     Called after the game updated a room's scan, also when that failed.
    /// </summary>
    /// <param name="simulated">What <see cref="BeforeScanUpdate" /> returned for this update.</param>
    public void ScanUpdated(bool simulated)
    {
        if (simulated)
        {
            powerConsumptionForwarder.ExitConsumer();
        }
    }

    /// <summary>
    ///     The room's scan target and progress as this client reports them to the server.
    /// </summary>
    public MapRoomMetadata GetMetadata(MapRoomFunctionality mapRoom) =>
        new(mapRoom.typeToScan.ToDto(), mapRoom.numNodesScanned, mapRoom.TryGetNitroxId(out NitroxId? roomId) ? GetGeneration(roomId) : 0);

    /// <summary>
    ///     Makes the room scan for what the server says it scans for.
    /// </summary>
    public void ApplyMetadata(MapRoomFunctionality mapRoom, MapRoomMetadata metadata)
    {
        if (!TryGetRoom(mapRoom, out IRoom? room))
        {
            Log.Error($"Cannot apply {metadata} to a Scanner Room without an id");
            return;
        }
        ApplyMetadata(room, metadata);
    }

    /// <summary>
    ///     Called after a player started or stopped a scan on the room's screen. The game starts and stops scans for
    ///     other reasons as well; those are not for the server.
    /// </summary>
    public void TargetChosen(MapRoomFunctionality mapRoom)
    {
        if (TryGetRoom(mapRoom, out IRoom? room))
        {
            TargetChosen(room);
        }
    }

    /// <summary>
    ///     Called after the game updated the room's hologram, which is where its scan reveals the next result.
    /// </summary>
    /// <param name="nodesScannedBefore">How many results the scan had revealed before that update.</param>
    public void BlipsUpdated(MapRoomFunctionality mapRoom, int nodesScannedBefore)
    {
        if (mapRoom.numNodesScanned != nodesScannedBefore && TryGetRoom(mapRoom, out IRoom? room))
        {
            NodeScanned(room);
        }
    }

    /// <summary>
    ///     Called when the room's object is destroyed.
    /// </summary>
    public void RoomDestroyed(MapRoomFunctionality mapRoom)
    {
        // Not looked up by id: the object is on its way out, and its id may already belong to the room built in its place.
        foreach (RoomState state in statesByRoomId.Values)
        {
            if (state.Room is GameRoom gameRoom && ReferenceEquals(gameRoom.MapRoom, mapRoom))
            {
                RoomDestroyed(gameRoom);
                return;
            }
        }
    }

    internal static bool ShouldSuppressPowerDrain(bool hasEntityId, bool hasOwnership) => hasEntityId && !hasOwnership;

    internal long GetGeneration(NitroxId roomId) => statesByRoomId.TryGetValue(roomId, out RoomState? state) ? state.Generation : 0;

    /// <summary>
    ///     Called every frame for every room that has an id. Subscribes to the room's scan, notices when this client
    ///     starts or stops simulating the room and when the room loses power, and sends the reports that are due.
    /// </summary>
    internal void Update(IRoom room)
    {
        RoomState state = GetState(room);
        if (!state.Subscribed)
        {
            // For as long as the room exists, not for as long as its screen is on: the HUD shows the results far from
            // the room, and a screen that is switched on again has to show what changed in between.
            Subscribe(room, state);
        }
        bool simulating = simulationOwnership.HasAnyLockType(room.Id);
        if (simulating != state.Simulating)
        {
            state.Simulating = simulating;
            if (simulating)
            {
                StartSimulating(room, state);
            }
            else
            {
                // What this client's game left in the room's list is not what the others see. The server answers a
                // subscription with its list.
                Subscribe(room, state);
            }
        }
        bool powered = room.Powered;
        if (simulating && state.Powered && !powered)
        {
            PowerLost(room);
        }
        state.Powered = powered;
        // Results are found for a target the server has confirmed. A target this client has just chosen is on its
        // way to the server; a report for it would name the generation of the previous target and be dropped.
        if (simulating && state.ReportDue && room.Target == state.Target)
        {
            state.ReportDue = false;
            ReportResults(room, state);
        }
    }

    /// <summary>
    ///     Applies the scan target and progress the server sent. The server sends them when they change, as the answer
    ///     to a request it refused, and with the room itself when the room is spawned.
    /// </summary>
    internal void ApplyMetadata(IRoom room, MapRoomMetadata metadata)
    {
        RoomState state = GetState(room);
        if (state.MetadataApplied && metadata.Generation < state.Generation)
        {
            // A room is spawned with the state it had when the server prepared the spawn. An update for it can
            // arrive, and be applied, before the spawn has finished.
            Log.Warn($"Ignoring outdated scan state of Scanner Room {room.Id}: generation {metadata.Generation}, known {state.Generation}");
            return;
        }

        TechType target = metadata.TypeToScan.ToUnity();
        bool newGeneration = !state.MetadataApplied || metadata.Generation != state.Generation;
        // The first time, the game is told even when the target already matches, so that it does what it does when a
        // scan stops or starts.
        bool newTarget = !state.MetadataApplied || room.Target != target;
        bool simulating = simulationOwnership.HasAnyLockType(room.Id);
        // The simulating player's game goes on scanning while a new target is on its way to the server and back. What
        // it revealed in that time is ahead of what the server knows.
        bool ahead = simulating && !newTarget && room.NodesScanned > metadata.NumNodesScanned;

        state.MetadataApplied = true;
        state.Generation = metadata.Generation;
        state.Target = target;
        state.NodesKnownToServer = metadata.NumNodesScanned;
        if (newGeneration && !simulating)
        {
            // What the room holds was found for the previous target.
            room.Nodes.Clear();
        }
        if (newTarget)
        {
            room.StartScanning(target);
        }
        if (!ahead)
        {
            room.NodesScanned = metadata.NumNodesScanned;
        }
        room.ShowResults();
        if (newTarget)
        {
            room.ShowTarget();
        }
        if (simulating && newGeneration)
        {
            state.ReportDue = true;
            if (ahead)
            {
                NodeScanned(room);
            }
        }
    }

    /// <summary>
    ///     Tells the server which target the room scans for now. The server answers everyone, this client included,
    ///     with the target it settles on and that target's generation.
    /// </summary>
    internal void TargetChosen(IRoom room)
    {
        // Progress of the previous target that still waits to be sent would arrive after the new target and be refused.
        throttledPacketSender.RemovePendingPackets(room.Id);
        packetSender.Send(new EntityMetadataUpdate(room.Id, new MapRoomMetadata(room.Target.ToDto(), room.NodesScanned, GetGeneration(room.Id))));
    }

    /// <summary>
    ///     Tells the server how many of the found resources the scan has revealed, if this client simulates the room
    ///     and the number is news to the server. The game also lowers its number, when results are gone. The server
    ///     refuses a lower number and answers with its own, which the game would lower again.
    /// </summary>
    internal void NodeScanned(IRoom room)
    {
        RoomState state = GetState(room);
        if (!simulationOwnership.HasAnyLockType(room.Id) || room.Target != state.Target || room.NodesScanned <= state.NodesKnownToServer)
        {
            return;
        }
        state.NodesKnownToServer = room.NodesScanned;
        throttledPacketSender.SendThrottled(new EntityMetadataUpdate(room.Id, new MapRoomMetadata(room.Target.ToDto(), room.NodesScanned, state.Generation)),
            update => update.Id, PROGRESS_THROTTLE_SECONDS);
    }

    /// <summary>
    ///     Forgets what is known about the room's scan and tells the server to stop sending it.
    /// </summary>
    internal void RoomDestroyed(IRoom room)
    {
        if (!statesByRoomId.TryGetValue(room.Id, out RoomState? state) || !ReferenceEquals(state.Room, room))
        {
            return;
        }
        statesByRoomId.Remove(room.Id);
        if (state.Subscribed)
        {
            packetSender.Send(new MapRoomScanSubscription(room.Id, false));
        }
    }

    /// <summary>
    ///     Stops the room's scan for everyone, as a player would on the room's screen. The game stops a scan when the
    ///     room loses power: it switches the room's screen off, and the screen takes the scan with it. That screen is
    ///     never allowed to (see uGUI_MapRoomScanner_OnDisable_Patch), because it is also switched off with a room that
    ///     is destroyed or starts without power, and in the game of every player. So the loss itself is acted on here,
    ///     by the one player whose game runs the scan.
    /// </summary>
    private void PowerLost(IRoom room)
    {
        // While this client joins or builds its bases anew, the power of a base goes and comes back as its sources are
        // brought in. The game itself calls a base powered while it loads, whatever its sources say.
        if (room.Target == TechType.None || !worldLoaded())
        {
            return;
        }
        room.StartScanning(TechType.None);
        room.ShowTarget();
        TargetChosen(room);
    }

    /// <summary>
    ///     Asks the server for what the room's scan has found, and for every change from now on. A packet can be held
    ///     back, as during a base resync; then this is tried again.
    /// </summary>
    private void Subscribe(IRoom room, RoomState state) => state.Subscribed = packetSender.Send(new MapRoomScanSubscription(room.Id, true));

    /// <summary>
    ///     The lists the room holds so far are the ones the server sent. From now on this client's own game says what
    ///     is in range, and the server is told: it holds nothing after a restart, and what the previous simulating
    ///     player had loaded is not what this one has.
    /// </summary>
    private void StartSimulating(IRoom room, RoomState state)
    {
        room.ObtainNodes();
        room.ShowResults();
        state.ReportDue = true;
        state.PublishedTypes = null;
        room.ObtainTypes();
        ReportTypes(room);
    }

    /// <summary>
    ///     What is known about this room's scan. A room that was built again has the id of the one before it and
    ///     starts from nothing: neither the generation nor the subscription of the old one are its own.
    /// </summary>
    private RoomState GetState(IRoom room)
    {
        if (!statesByRoomId.TryGetValue(room.Id, out RoomState? state) || !ReferenceEquals(state.Room, room))
        {
            state = new RoomState(room);
            statesByRoomId[room.Id] = state;
        }
        return state;
    }

    private bool TryGetRoom(MapRoomFunctionality mapRoom, out IRoom? room)
    {
        if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId? roomId) || !IsRoomOf(roomId, mapRoom))
        {
            room = null;
            return false;
        }
        room = GetRoom(mapRoom, roomId);
        return true;
    }

    /// <summary>
    ///     A room that is being replaced keeps its id until its object is destroyed. From the moment the id leads to
    ///     the new object, the old one is no longer the room.
    /// </summary>
    private static bool IsRoomOf(NitroxId roomId, MapRoomFunctionality mapRoom) =>
        NitroxEntity.TryGetObjectFrom(roomId, out GameObject gameObject) && gameObject == mapRoom.gameObject;

    private bool TryGetRoom(NitroxId roomId, out IRoom? room)
    {
        if (!NitroxEntity.TryGetObjectFrom(roomId, out GameObject gameObject) || !gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            room = null;
            return false;
        }
        room = GetRoom(mapRoom, roomId);
        return true;
    }

    private IRoom GetRoom(MapRoomFunctionality mapRoom, NitroxId roomId) =>
        statesByRoomId.TryGetValue(roomId, out RoomState? state) && state.Room is GameRoom gameRoom && ReferenceEquals(gameRoom.MapRoom, mapRoom)
            ? gameRoom
            : new GameRoom(mapRoom, roomId);

    private sealed class RoomState(IRoom room)
    {
        /// <summary>
        ///     The room this is about. Kept so that asking for the game's room every frame creates nothing, and to
        ///     tell a room from one that was built in its place.
        /// </summary>
        public IRoom Room => room;

        /// <summary>
        ///     The generation of the scan target the server last sent, see <see cref="MapRoomMetadata.Generation" />.
        /// </summary>
        public long Generation;

        /// <summary>
        ///     The scan target the server last sent. The room's own target differs from it while a target this client
        ///     chose is on its way to the server.
        /// </summary>
        public TechType Target;

        /// <summary>
        ///     How many revealed results the server last sent, or was last sent by this client, for the current target.
        /// </summary>
        public int NodesKnownToServer;

        public bool MetadataApplied;
        public bool Subscribed;

        /// <summary>
        ///     Whether this client simulated the room when it last looked.
        /// </summary>
        public bool Simulating;

        /// <summary>
        ///     Whether the room had power when this client last looked. False for a room it has not looked at yet:
        ///     power that is missing when a room starts existing was not lost, the game may just not have connected
        ///     the room to its base yet.
        /// </summary>
        public bool Powered;

        /// <summary>
        ///     Whether the server has to be sent the room's full list of results.
        /// </summary>
        public bool ReportDue;

        public bool TypesReceived;
        public readonly HashSet<TechType> AvailableTypes = [];

        /// <summary>
        ///     The types last reported to the server, with the scan range and the time they were reported at. Null
        ///     when the next report must be sent whatever it contains.
        /// </summary>
        public HashSet<TechType>? PublishedTypes;

        public float PublishedRange;
        public float PublishedTime;
    }
}
