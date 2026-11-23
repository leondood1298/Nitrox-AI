namespace Nitrox.Server.Subnautica.Models.Serialization.World;

/// <summary>
///     The world cannot be opened. The message is meant for the user as it is; the server stops without a stack trace.
/// </summary>
internal sealed class WorldLoadException(string message) : Exception(message);
