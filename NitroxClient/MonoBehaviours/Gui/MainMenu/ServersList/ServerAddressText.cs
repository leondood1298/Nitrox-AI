namespace NitroxClient.MonoBehaviours.Gui.MainMenu.ServersList;

/// <summary>
///     A server's address as the player sees it on screen. With "hide IP" on (for streaming) no screen shows it.
/// </summary>
public static class ServerAddressText
{
    public const string HIDDEN = "***.***.***.***:*****";

    public static string Format(string address, int port, bool hide)
    {
        return hide ? HIDDEN : $"{address}:{port}";
    }
}
