using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Everything the server holds about a Scanner Room camera besides where it is: its light, battery and health, its
///     number, and the dock it sits in. It stays with the camera through its three forms: docked (a child of its room),
///     loose in the world, and carried in an inventory.
/// </summary>
[Serializable]
[DataContract]
public class MapRoomCameraMetadata : EntityMetadata
{
    public const float MAX_ENERGY = 100f;
    public const float MAX_HEALTH = 400f;

    /// <summary>
    ///     A Scanner Room has a left and a right camera dock, with docking index 0 and 1.
    /// </summary>
    public const int DOCK_COUNT = 2;

    public const int NOT_DOCKED = -1;

    [DataMember(Order = 1)]
    public bool LightOn { get; }

    [DataMember(Order = 2)]
    public float Energy { get; }

    [DataMember(Order = 3)]
    public float Health { get; }

    /// <summary>
    ///     The number the camera shows on every screen. The game has one list of all cameras, and any screen cycles
    ///     through all of them, so the number is unique in the world. The server assigns it once; 0 until then.
    /// </summary>
    [DataMember(Order = 4)]
    public int CameraNumber { get; }

    /// <summary>
    ///     The dock of its parent room the camera sits in, or <see cref="NOT_DOCKED" />. Only the server changes it.
    /// </summary>
    [DataMember(Order = 5)]
    public int DockingIndex { get; }

    public bool IsDocked => DockingIndex >= 0;

    [IgnoreConstructor]
    protected MapRoomCameraMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public MapRoomCameraMetadata(bool lightOn = false, float energy = MAX_ENERGY, float health = MAX_HEALTH, int cameraNumber = 0, int dockingIndex = NOT_DOCKED)
    {
        LightOn = lightOn;
        Energy = energy;
        Health = health;
        CameraNumber = cameraNumber;
        DockingIndex = dockingIndex;
    }

    public MapRoomCameraMetadata WithLight(bool lightOn) => new(lightOn, Energy, Health, CameraNumber, DockingIndex);

    public MapRoomCameraMetadata WithComponents(float energy, float health) => new(LightOn, energy, health, CameraNumber, DockingIndex);

    public MapRoomCameraMetadata WithCameraNumber(int cameraNumber) => new(LightOn, Energy, Health, cameraNumber, DockingIndex);

    public MapRoomCameraMetadata WithDockingIndex(int dockingIndex) => new(LightOn, Energy, Health, CameraNumber, dockingIndex);

    /// <summary>
    ///     Coarse percentage band (0, 10, 25, 50, 75 or 100) of a camera's energy or health relative to its maximum.
    ///     Server and client report a component change only when it crosses into another band.
    /// </summary>
    public static int GetComponentBand(float value, float maximum) => value switch
    {
        <= 0f => 0,
        _ when value <= maximum * 0.1f => 10,
        _ when value <= maximum * 0.25f => 25,
        _ when value <= maximum * 0.5f => 50,
        _ when value <= maximum * 0.75f => 75,
        _ => 100
    };

    public override string ToString()
    {
        return $"[{nameof(MapRoomCameraMetadata)} Number: {CameraNumber}, DockingIndex: {DockingIndex}, LightOn: {LightOn}, Energy: {Energy}, Health: {Health}]";
    }
}
