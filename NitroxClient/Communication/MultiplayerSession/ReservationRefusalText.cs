using System;
using System.Collections.Generic;
using System.Text;
using Nitrox.Model.MultiplayerSession;

namespace NitroxClient.Communication.MultiplayerSession;

/// <summary>
///     The text a refused player reads, in their own language: each refusal reason is a language key here rather than the
///     server's English description.
/// </summary>
public static class ReservationRefusalText
{
    private static readonly (MultiplayerSessionReservationState Reason, string Key)[] keysByReason =
    [
        (MultiplayerSessionReservationState.UNIQUE_PLAYER_NAME_CONSTRAINT_VIOLATED, "Nitrox_Refused_NameInUse"),
        (MultiplayerSessionReservationState.SERVER_PLAYER_CAPACITY_REACHED, "Nitrox_Refused_ServerFull"),
        (MultiplayerSessionReservationState.AUTHENTICATION_FAILED, "Nitrox_Refused_WrongPassword"),
        (MultiplayerSessionReservationState.HARDCORE_PLAYER_DEAD, "Nitrox_Refused_HardcoreDead"),
        (MultiplayerSessionReservationState.INCORRECT_USERNAME, "Nitrox_Refused_InvalidName"),
        (MultiplayerSessionReservationState.PLAYER_BANNED, "Nitrox_Refused_Banned"),
        (MultiplayerSessionReservationState.PLAYER_KEY_MISMATCH, "Nitrox_Refused_KeyMismatch")
    ];

    public static IEnumerable<string> Keys
    {
        get
        {
            foreach ((MultiplayerSessionReservationState _, string key) in keysByReason)
            {
                yield return key;
            }
        }
    }

    /// <param name="translate">Turns a language key into the player's text.</param>
    /// <param name="rejectionReason">Extra detail the server gave, such as a ban's reason; shown as it came.</param>
    public static string Describe(MultiplayerSessionReservationState state, string? rejectionReason, Func<string, string> translate)
    {
        StringBuilder text = new();
        foreach ((MultiplayerSessionReservationState reason, string key) in keysByReason)
        {
            if (state.HasStateFlag(reason))
            {
                text.AppendLine(translate(key));
            }
        }
        if (text.Length == 0)
        {
            text.AppendLine(translate("Nitrox_Refused_Unknown"));
        }
        if (!string.IsNullOrEmpty(rejectionReason))
        {
            text.Append(rejectionReason);
        }
        return text.ToString().TrimEnd();
    }
}
