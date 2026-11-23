using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The room in the game, as far as its scan is concerned.
/// </summary>
public sealed partial class MapRoomScans
{
    private static void ShowResults(MapRoomFunctionality mapRoom)
    {
        ShowHologramBlips(mapRoom);
        // The game collects the resources for the HUD every ten seconds. A resource that is gone would stay on the HUD
        // until then.
        uGUI_ResourceTracker resourceTracker = Object.FindObjectOfType<uGUI_ResourceTracker>();
        if (resourceTracker)
        {
            resourceTracker.gatherNextTick = true;
        }
    }

    /// <summary>
    ///     Shows a blip in the room's hologram for every result the scan has revealed, as the game does when its scan
    ///     reveals the next one.
    /// </summary>
    private static void ShowHologramBlips(MapRoomFunctionality mapRoom)
    {
        if (!mapRoom.mapBlipRoot)
        {
            return;
        }

        int visibleCount = Mathf.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
        Vector3 origin = mapRoom.mapBlipRoot.transform.position;
        for (int i = 0; i < visibleCount; i++)
        {
            Vector3 localPosition = (mapRoom.resourceNodes[i].position - origin) * mapRoom.mapScale;
            if (i >= mapRoom.mapBlips.Count)
            {
                GameObject blip = Object.Instantiate(mapRoom.blipPrefab);
                blip.transform.SetParent(mapRoom.mapBlipRoot.transform, false);
                mapRoom.mapBlips.Add(blip);
            }
            mapRoom.mapBlips[i].transform.localPosition = localPosition;
            mapRoom.mapBlips[i].SetActive(true);
        }
        for (int i = visibleCount; i < mapRoom.mapBlips.Count; i++)
        {
            mapRoom.mapBlips[i].SetActive(false);
        }
    }

    private static void RemoveHudNode(string resourceId, TechType techType, Vector3 position)
    {
        uGUI_ResourceTracker resourceTracker = Object.FindObjectOfType<uGUI_ResourceTracker>();
        if (!resourceTracker)
        {
            return;
        }
        RemoveFromSet(resourceTracker.nodes, resourceId, techType, position);
        resourceTracker.gatherNextTick = true;
        resourceTracker.UpdateBlips();
    }

    /// <summary>
    ///     What this class reads from and changes on a room. The rules above only use this, so that they can be tested
    ///     without the game running.
    /// </summary>
    internal interface IRoom
    {
        NitroxId Id { get; }

        /// <summary>
        ///     What the room scans for in this client's game.
        /// </summary>
        TechType Target { get; }

        /// <summary>
        ///     How many of <see cref="Nodes" /> the scan has revealed. The game reveals them one by one, in order.
        /// </summary>
        int NodesScanned { get; set; }

        /// <summary>
        ///     What the scan has found.
        /// </summary>
        List<ResourceTrackerDatabase.ResourceInfo> Nodes { get; }

        Vector3 ScanOrigin { get; }
        float ScanRange { get; }

        /// <summary>
        ///     The types the room's screen lists. Null if the room has no screen.
        /// </summary>
        HashSet<TechType>? ShownTypes { get; }

        /// <summary>
        ///     Every type the game can scan for at all.
        /// </summary>
        IEnumerable<TechType> DetectableTypes { get; }

        /// <summary>
        ///     Starts a scan, or stops scanning, the way the game does it.
        /// </summary>
        void StartScanning(TechType target);

        /// <summary>
        ///     Fills <see cref="Nodes" /> with what this client's game has loaded in range.
        /// </summary>
        void ObtainNodes();

        /// <summary>
        ///     Fills <see cref="ShownTypes" /> with the types of what this client's game has loaded in range.
        /// </summary>
        void ObtainTypes();

        /// <summary>
        ///     Makes the hologram and the HUD show <see cref="Nodes" /> as far as they are revealed.
        /// </summary>
        void ShowResults();

        /// <summary>
        ///     Makes the room's screen show <see cref="Target" />.
        /// </summary>
        void ShowTarget();

        /// <summary>
        ///     Makes the room's screen list these types.
        /// </summary>
        void ShowTypes(IEnumerable<TechType> types);
    }

    private sealed class GameRoom(MapRoomFunctionality mapRoom, NitroxId id) : IRoom
    {
        private uGUI_MapRoomScanner? scanner;

        public MapRoomFunctionality MapRoom => mapRoom;
        public NitroxId Id => id;
        public TechType Target => mapRoom.typeToScan;

        public int NodesScanned
        {
            get => mapRoom.numNodesScanned;
            set => mapRoom.numNodesScanned = value;
        }

        public List<ResourceTrackerDatabase.ResourceInfo> Nodes => mapRoom.resourceNodes;
        public Vector3 ScanOrigin => mapRoom.wireFrameWorld.position;
        public float ScanRange => mapRoom.GetScanRange();
        public HashSet<TechType>? ShownTypes => Scanner ? Scanner.availableTechTypes : null;
        public IEnumerable<TechType> DetectableTypes => ResourceTrackerDatabase.GetDetectableTechTypes();

        private uGUI_MapRoomScanner Scanner
        {
            get
            {
                if (!scanner)
                {
                    scanner = mapRoom.GetComponentInChildren<uGUI_MapRoomScanner>(true);
                }
                return scanner;
            }
        }

        public void StartScanning(TechType target) => mapRoom.StartScanning(target);

        public void ObtainNodes() => mapRoom.ObtainResourceNodes(mapRoom.typeToScan);

        public void ObtainTypes()
        {
            if (Scanner)
            {
                Scanner.UpdateAvailableTechTypes();
            }
        }

        public void ShowResults() => MapRoomScans.ShowResults(mapRoom);

        public void ShowTarget()
        {
            if (Scanner)
            {
                Scanner.UpdateGUIState();
            }
        }

        public void ShowTypes(IEnumerable<TechType> types)
        {
            if (!Scanner)
            {
                return;
            }
            Scanner.availableTechTypes.Clear();
            Scanner.availableTechTypes.UnionWith(types);
            Scanner.currentPage = Mathf.Clamp(Scanner.currentPage, 0, Mathf.Max(0, Scanner.numPages - 1));
            Scanner.RebuildResourceList();
        }
    }
}
