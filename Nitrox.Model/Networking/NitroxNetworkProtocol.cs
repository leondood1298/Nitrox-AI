using System;
using System.Globalization;

namespace Nitrox.Model.Networking;

public static class NitroxNetworkProtocol
{
    private const string CONNECTION_KEY_PREFIX = "nitrox-ai/";

    /// <summary>
    ///     Increment before shipping an incompatible packet type or schema change.
    /// </summary>
    public const int Epoch = 2;

    public static string ConnectionKey { get; } = $"{CONNECTION_KEY_PREFIX}{Epoch}";

    public static bool IsCompatible(string? connectionKey) =>
        string.Equals(connectionKey, ConnectionKey, StringComparison.Ordinal);

    public static bool TryGetEpoch(string? connectionKey, out int epoch)
    {
        epoch = 0;
        if (connectionKey == null || !connectionKey.StartsWith(CONNECTION_KEY_PREFIX, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(connectionKey.Substring(CONNECTION_KEY_PREFIX.Length), NumberStyles.None, CultureInfo.InvariantCulture, out epoch) && epoch >= 0;
    }
}
