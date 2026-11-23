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
///     its scan in their own game and reports what it finds; everybody else shows what the server sends.
/// </summary>
public sealed partial class MapRoomScans
{
    private const float PROGRESS_THROTTLE_SECONDS = 0.2f;

    private readonly IPacketSender packetSender;
    private readonly ThrottledPacketSender throttledPacketSender;
    private readonly SimulationOwnership simulationOwnership;
    private readonly Func<float> secondsSinceStartup;

    /// <summary>
    ///     What this client knows about a room's scan that the game does not keep on the room itself.
    /// </summary>
    private readonly Dictionary<NitroxId, RoomState> statesByRoomId = [];

    public MapRoomScans(IPacketSender packetSender, ThrottledPacketSender throttledPacketSender, SimulationOwnership simulationOwnership)
        : this(packetSender, throttledPacketSender, simulationOwnership, static () => Time.realtimeSinceStartup)
    {
    }

    internal MapRoomScans(IPacketSender packetSender, ThrottledPacketSender throttledPacketSender, SimulationOwnership simulationOwnership,
        Func<float> secondsSinceStartup)
    {
        this.packetSender = packetSender;
        this.throttledPacketSender = throttledPacketSender;
        this.simulationOwnership = simulationOwnership;
        this.secondsSinceStartup = secondsSinceStartup;
    }

    /// <summary>
    ///     Called every frame for every room that has an id. Notices when this client starts or stops simulating the
    ///     room, and sends the reports that are due.
    /// </summary>
    public void Update(MapRoomFunctionality mapRoom, NitroxId roomId) => Update(GetRoom(mapRoom, roomId));

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
    ///     Called after the game started or stopped a scan in this room.
    /// </summary>
    public void TargetChosen(MapRoomFunctionality mapRoom)
    {
        if (TryGetRoom(mapRoom, out IRoom? room))
        {
            TargetChosen(room);
        }
    }

    /// <summary>
    ///     Called after the number of results the room's scan has revealed changed in this client's game.
    /// </summary>
    public void NodeScanned(MapRoomFunctionality mapRoom)
    {
        if (TryGetRoom(mapRoom, out IRoom? room))
        {
            NodeScanned(room);
        }
    }

    /// <summary>
    ///     Asks the server for what the room's scan has found, and for every change from now on.
    /// </summary>
    public void Subscribe(uGUI_MapRoomScanner scanner)
    {
        if (scanner && TryGetRoom(scanner.mapRoom, out IRoom? room))
        {
            Subscribe(room);
        }
    }

    public void Unsubscribe(uGUI_MapRoomScanner scanner)
    {
        if (scanner && TryGetRoom(scanner.mapRoom, out IRoom? room))
        {
            Unsubscribe(room);
        }
    }

    internal long GetGeneration(NitroxId roomId) => statesByRoomId.TryGetValue(roomId, out RoomState? state) ? state.Generation : 0;

    internal void Update(IRoom room)
    {
        RoomState state = GetState(room.Id);
        bool simulating = simulationOwnership.HasAnyLockType(room.Id);
        if (simulating != state.Simulating)
        {
            state.Simulating = simulating;
            if (simulating)
            {
                StartSimulating(room, state);
            }
            else if (state.Subscribed)
            {
                // What this client's game left in the room's list is not what the others see. The server answers a
                // subscription with its list.
                Subscribe(room);
            }
        }
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
        RoomState state = GetState(room.Id);
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
        if (newGeneration)
        {
            state.ResultsReceived = false;
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
        RoomState state = GetState(room.Id);
        if (!simulationOwnership.HasAnyLockType(room.Id) || room.Target != state.Target || room.NodesScanned <= state.NodesKnownToServer)
        {
            return;
        }
        state.NodesKnownToServer = room.NodesScanned;
        throttledPacketSender.SendThrottled(new EntityMetadataUpdate(room.Id, new MapRoomMetadata(room.Target.ToDto(), room.NodesScanned, state.Generation)),
            update => update.Id, PROGRESS_THROTTLE_SECONDS);
    }

    internal void Subscribe(IRoom room) => GetState(room.Id).Subscribed = packetSender.Send(new MapRoomScanSubscription(room.Id, true));

    internal void Unsubscribe(IRoom room)
    {
        packetSender.Send(new MapRoomScanSubscription(room.Id, false));
        GetState(room.Id).Subscribed = false;
    }

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

    private RoomState GetState(NitroxId roomId)
    {
        if (!statesByRoomId.TryGetValue(roomId, out RoomState? state))
        {
            state = new RoomState();
            statesByRoomId[roomId] = state;
        }
        return state;
    }

    private bool TryGetRoom(MapRoomFunctionality mapRoom, out IRoom? room)
    {
        if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId? roomId))
        {
            room = null;
            return false;
        }
        room = GetRoom(mapRoom, roomId);
        return true;
    }

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

    private IRoom GetRoom(MapRoomFunctionality mapRoom, NitroxId roomId)
    {
        RoomState state = GetState(roomId);
        if (state.Room is not GameRoom gameRoom || !ReferenceEquals(gameRoom.MapRoom, mapRoom))
        {
            state.Room = new GameRoom(mapRoom, roomId);
        }
        return state.Room;
    }

    private sealed class RoomState
    {
        /// <summary>
        ///     The room in the game, kept so that asking for it every frame creates nothing.
        /// </summary>
        public IRoom? Room;

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
        ///     Whether the server has to be sent the room's full list of results.
        /// </summary>
        public bool ReportDue;

        /// <summary>
        ///     Whether the server has sent results for the current target.
        /// </summary>
        public bool ResultsReceived;

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
