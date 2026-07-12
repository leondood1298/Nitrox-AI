using UnityEngine;

namespace NitroxClient.GameLogic.Simulation;

public sealed class StalkerCameraGrab(CollectShiny collectShiny, GameObject camera) : LockRequestContext
{
    public CollectShiny CollectShiny { get; } = collectShiny;
    public GameObject Camera { get; } = camera;
}
