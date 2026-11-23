using System.Reflection;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     All the game does here is give the dock a new camera, unless the dock was loaded from a save. In multiplayer the
///     cameras of a room are entities the server sends: a room that was restored would get a second camera for every
///     dock whose camera is parked outside, and a room that was just built would get cameras that every client names
///     differently. So this never runs; the player who builds a room makes its two cameras once
///     (<see cref="NitroxClient.GameLogic.MapRoomCameraDocks.CreateDefaultCameras" />).
/// </summary>
public sealed partial class MapRoomCameraDocking_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCameraDocking t) => t.Start());

    public static bool Prefix() => false;
}
