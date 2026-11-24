namespace Nitrox.Server.Subnautica.Models.Serialization.World;

/// <summary>
///     The world cannot be opened.
/// </summary>
internal sealed class WorldLoadException(string message) : StartupFailureException(message, WORLD_LOAD_FAILED);
