using System.Diagnostics.CodeAnalysis;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Decides whether the server stores the timers of a water filtration machine. Only the player simulating the
///     machine runs them; anyone else's copy is what they were told, and one sent late by the previous simulating
///     player would put progress back.
/// </summary>
internal static class FiltrationMachineMetadataAuthority
{
    /// <summary>
    ///     How far a reported timer may lie above its start value and still be taken (it is clamped). A guess at more
    ///     than float rounding.
    /// </summary>
    private const float TOLERANCE = 0.01f;

    /// <param name="accepted">The timers to store: within range, a stopped one as <see cref="FiltrationMachineMetadata.IDLE" />.</param>
    public static bool TryAccept(Entity machine, FiltrationMachineMetadata requested, bool fromSimulatingPlayer, [NotNullWhen(true)] out FiltrationMachineMetadata? accepted)
    {
        accepted = null;
        if (!fromSimulatingPlayer || !IsFiltrationMachine(machine) ||
            !IsPossible(requested.TimeRemainingWater, FiltrationMachineMetadata.WATER_SECONDS) || !IsPossible(requested.TimeRemainingSalt, FiltrationMachineMetadata.SALT_SECONDS))
        {
            return false;
        }
        accepted = new FiltrationMachineMetadata(WithinRange(requested.TimeRemainingWater, FiltrationMachineMetadata.WATER_SECONDS),
                                                 WithinRange(requested.TimeRemainingSalt, FiltrationMachineMetadata.SALT_SECONDS));
        return true;
    }

    /// <summary>
    ///     The machine is a module inside a base room. Its entity may carry no tech type to check against.
    /// </summary>
    internal static bool IsFiltrationMachine(Entity entity) => entity is InteriorPieceEntity && entity.TechType?.Name is null or "None" or "BaseFiltrationMachine" or "FiltrationMachine";

    private static bool IsPossible(float timeRemaining, float startValue) => float.IsFinite(timeRemaining) && timeRemaining <= startValue + TOLERANCE;

    private static float WithinRange(float timeRemaining, float startValue) => timeRemaining > 0f ? Math.Min(timeRemaining, startValue) : FiltrationMachineMetadata.IDLE;
}
