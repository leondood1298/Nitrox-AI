using System;

namespace NitroxClient.GameLogic.Simulation;

/// <summary>
///     What the game was about to do when a Scanner Room camera's lock was asked for: give the player a camera of a
///     screen, by a click or by cycling.
/// </summary>
public sealed class CameraControlStart(MapRoomScreen screen, Action runVanilla) : LockRequestContext
{
    public MapRoomScreen Screen { get; } = screen;
    public Action RunVanilla { get; } = runVanilla;
}
