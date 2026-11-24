namespace NitroxClient.MonoBehaviours.Discord;

/// <summary>
///     The join secret a game publishes on Discord: the server's address and port, as "address:port". An IPv6 address
///     holds colons itself, so the port is what follows the last one.
/// </summary>
public static class DiscordJoinSecret
{
    public static bool TryParse(string secret, out string address, out int port)
    {
        address = string.Empty;
        port = 0;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        int lastColon = secret.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == secret.Length - 1)
        {
            return false;
        }
        if (!int.TryParse(secret.Substring(lastColon + 1), out port) || port is < 1 or > ushort.MaxValue)
        {
            port = 0;
            return false;
        }

        address = secret.Substring(0, lastColon);
        return true;
    }
}
