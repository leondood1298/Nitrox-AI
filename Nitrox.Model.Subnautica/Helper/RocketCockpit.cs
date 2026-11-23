using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.Helper;

/// <summary>
///     Who counts as aboard the rocket. The server decides whether a launch may start and each client decides whether
///     its own player takes part, so both use this one rule.
/// </summary>
public static class RocketCockpit
{
    /// <summary>
    ///     Radius in metres of the sphere around the rocket's "AtmosphereVolume" child, which sits in the middle of the
    ///     cockpit (stage 3). Taken over unchanged from Nitrox's launch code, which calls it a "pre-calculated radius";
    ///     how it was found is not recorded and it was not re-measured. It has decided who takes part in the ending
    ///     since multiplayer launches exist, so players in the cockpit are known to be within it. The platform's deck
    ///     and the elevator's foot lie two stages lower, far outside it.
    /// </summary>
    public const float RADIUS = 5.55f;

    /// <summary>
    ///     The furthest the cockpit centre may lie from the rocket entity's own position. The real offset is fixed prefab
    ///     data the server cannot read: an estimate (not measured) of a few tens of metres, the platform plus the stages
    ///     under the cockpit. The bound is deliberately generous: it only refuses a centre that cannot belong to this
    ///     rocket at all, and tolerates a stored rocket position that differs from where the platform settled.
    /// </summary>
    public const float MAX_DISTANCE_FROM_ROCKET = 100f;

    public static bool IsAboard(NitroxVector3 cockpitCenter, NitroxVector3 playerPosition)
    {
        return NitroxVector3.Distance(cockpitCenter, playerPosition) <= RADIUS;
    }

    public static bool CanBeCockpitOf(NitroxVector3 rocketPosition, NitroxVector3 cockpitCenter)
    {
        return NitroxVector3.Distance(rocketPosition, cockpitCenter) <= MAX_DISTANCE_FROM_ROCKET;
    }
}
