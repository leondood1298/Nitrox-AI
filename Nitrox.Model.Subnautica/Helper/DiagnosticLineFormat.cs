using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.Helper;

/// <summary>
///     The single-line shape shared by the Scanner Room and base power diagnostics, on the server and on the client.
///     Every line starts with the same fields in the same order (<c>[TAG] n= ep= side= sid= ev= out=</c>), followed by
///     the feature's own fields and ending in <c>reason=</c>. One grep pattern therefore works across the server log
///     and both client logs, which is what a tester needs to follow one request through all three.
/// </summary>
public static class DiagnosticLineFormat
{
    public const string SCANNER_ROOM_TAG = "[SRD1]";
    public const string BASE_POWER_TAG = "[BPD1]";
    public const char SERVER_SIDE = 'S';
    public const char CLIENT_SIDE = 'C';

    /// <summary>
    ///     Printed for a field that does not apply to the line, so the fields keep their positions.
    /// </summary>
    public const string NOT_APPLICABLE = "-";

    public const int DEFAULT_TOKEN_LENGTH = 48;

    // Seeded randomly so that epochs of different processes (server, client A, client B) do not collide.
    private static int nextEpoch = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);

    /// <summary>
    ///     A new id for one run of a diagnostics instance. Sequence numbers restart with every epoch (server start or
    ///     client join), so a line is only unique by epoch and sequence number together.
    /// </summary>
    public static string NewEpoch() => unchecked((uint)Interlocked.Increment(ref nextEpoch)).ToString("x8", CultureInfo.InvariantCulture);

    /// <summary>
    ///     The fields every diagnostic line starts with.
    /// </summary>
    /// <param name="sequence">Null for marker lines, which are not events and must not leave gaps in the numbering.</param>
    /// <param name="sessionId">The player the line is about: the sender on the server, the local player on a client.</param>
    public static string Header(string tag, long? sequence, string epoch, char side, SessionId? sessionId, string eventName, string outcome) =>
        $"{tag} n={Number(sequence)} ep={epoch} side={side} sid={Session(sessionId)} ev={Token(eventName)} out={Token(outcome)}";

    public static string Session(SessionId? sessionId) =>
        sessionId is { } value ? ((ushort)value).ToString(CultureInfo.InvariantCulture) : NOT_APPLICABLE;

    public static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? NOT_APPLICABLE;

    public static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? NOT_APPLICABLE;

    /// <summary>
    ///     Wall clock time for marker lines. The server and each client stamp their log lines with their own local
    ///     time; a UTC stamp on both sides is what lets the three files be lined up.
    /// </summary>
    public static string UtcStamp(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Reduces free text to one lower-case word, so a value can never break a line into two or add a fake field.
    /// </summary>
    public static string Token(string? value, int maxLength = DEFAULT_TOKEN_LENGTH)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return NOT_APPLICABLE;
        }

        StringBuilder token = new(Math.Min(value.Length, maxLength));
        foreach (char character in value)
        {
            char normalized = char.ToLowerInvariant(character);
            if (normalized is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.')
            {
                token.Append(normalized);
            }
            else if (token.Length > 0 && token[token.Length - 1] != '_')
            {
                token.Append('_');
            }

            if (token.Length == maxLength)
            {
                break;
            }
        }
        string result = token.ToString().Trim('_');
        return result.Length > 0 ? result : NOT_APPLICABLE;
    }

    /// <summary>
    ///     Shortens an id for a log line.
    /// </summary>
    /// <param name="includeFourthGroup">
    ///     The cameras of one Scanner Room get ids derived from the room's id and share their first group, as can the
    ///     pieces of one base. The first half of the fourth group keeps such siblings apart.
    /// </param>
    public static string ShortId(NitroxId? id, bool includeFourthGroup)
    {
        string value = id?.ToString() ?? NOT_APPLICABLE;
        if (includeFourthGroup && value.Length >= 23 && value[8] == '-' && value[13] == '-' && value[18] == '-')
        {
            return $"{value.Substring(0, 8)}.{value.Substring(19, 4)}";
        }
        return value.Length > 8 ? value.Substring(0, 8) : value;
    }
}
