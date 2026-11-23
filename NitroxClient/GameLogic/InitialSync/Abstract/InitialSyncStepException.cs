using System;

namespace NitroxClient.GameLogic.InitialSync.Abstract;

/// <summary>
///     A step of the initial sync failed, so the world could not be set up from the snapshot.
/// </summary>
public sealed class InitialSyncStepException(string processorName, int stepIndex, Exception inner)
    : Exception($"{processorName} failed at step {stepIndex + 1}: {inner.Message}", inner)
{
    /// <summary>
    ///     Which part of the setup failed, for the player's message.
    /// </summary>
    public string Step { get; } = $"{processorName} {stepIndex + 1}";
}
