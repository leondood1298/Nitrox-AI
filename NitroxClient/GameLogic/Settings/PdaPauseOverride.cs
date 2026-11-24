using System;

namespace NitroxClient.GameLogic.Settings;

/// <summary>
///     Keeps the PDA from pausing the game during a multiplayer session without touching the player's own PDA pause
///     setting, which the game saves in its options file and which single-player play keeps using.
/// </summary>
public sealed class PdaPauseOverride(Func<bool> getPdaPause, Action<bool> setPdaPause)
{
    public static PdaPauseOverride Game { get; } = new(() => MiscSettings.pdaPause, value => MiscSettings.pdaPause = value);

    private bool? playerValue;

    public bool IsActive => playerValue.HasValue;

    public void Begin()
    {
        if (playerValue.HasValue)
        {
            return;
        }
        playerValue = getPdaPause();
        setPdaPause(false);
    }

    public void End()
    {
        if (!playerValue.HasValue)
        {
            return;
        }
        setPdaPause(playerValue.Value);
        playerValue = null;
    }

    /// <summary>
    ///     The options file is read and written in one method; it must see the player's value either way.
    /// </summary>
    public void BeforeSettingsSerialized()
    {
        if (playerValue.HasValue)
        {
            setPdaPause(playerValue.Value);
        }
    }

    public void AfterSettingsSerialized()
    {
        if (playerValue.HasValue)
        {
            // When the file was read, this is what it held; when written, it is unchanged.
            playerValue = getPdaPause();
            setPdaPause(false);
        }
    }
}
