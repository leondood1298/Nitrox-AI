using Nitrox.Model.DataStructures;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What <see cref="ScannerRoomStateFingerprint.Create" /> read from a room: the digest, and next to it the values
///     the diagnostics print, so that a digest that changed can be explained from the log alone.
/// </summary>
/// <param name="CanonicalState">The text the digest was made of.</param>
internal readonly record struct ScannerRoomStateSnapshot(
    string Fingerprint,
    string CanonicalState,
    int CameraCount,
    string ScanTarget,
    int NodesScanned,
    long MetadataGeneration,
    bool FabricatorActive,
    NitroxId? LeftDockCameraId = null,
    NitroxId? RightDockCameraId = null);
