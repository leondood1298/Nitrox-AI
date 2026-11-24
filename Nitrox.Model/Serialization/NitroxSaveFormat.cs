namespace Nitrox.Model.Serialization;

/// <summary>
///     The fork's own number for the layout of a saved world, kept in Version.json next to the Nitrox version.
/// </summary>
/// <remarks>
///     The Nitrox version stays the same across builds of this fork, so it cannot tell two builds' worlds apart. A world
///     of another format is refused rather than converted: a half-understood world loads with silently missing or
///     defaulted data.
/// </remarks>
public static class NitroxSaveFormat
{
    /// <summary>
    ///     Increment whenever saved data changes so that worlds saved before can no longer be read correctly.
    /// </summary>
    /// <remarks>
    ///     1: the world is saved as one generation with a manifest (SaveManifest.json).
    ///     2: child entities are stored once, inside their parent, and a player is tied to a public key. A world of format
    ///     1 is still read, and is written as format 2 from then on; a build that only knows format 1 would lose the nested
    ///     entities, so it must refuse such a world.
    /// </remarks>
    public const int CURRENT = 2;

    /// <summary>
    ///     The oldest format this build still reads.
    /// </summary>
    public const int OLDEST_READABLE = 1;

    /// <summary>
    ///     A Version.json without the number was written by a build from before the number existed.
    /// </summary>
    public const int UNKNOWN = 0;

    /// <summary>
    ///     Whether this build reads a world of <paramref name="worldFormat" />.
    /// </summary>
    public static bool CanRead(int worldFormat) => worldFormat is >= OLDEST_READABLE and <= CURRENT;

    /// <summary>
    ///     Explains why a world of <paramref name="worldFormat" /> cannot be loaded, or null when it can.
    /// </summary>
    public static string? DescribeMismatch(string worldName, int worldFormat)
    {
        if (CanRead(worldFormat))
        {
            return null;
        }
        string age = worldFormat < OLDEST_READABLE ? "an older" : "a newer";
        return $"The world \"{worldName}\" was saved by {age} build of Nitrox (save format {worldFormat}; this build reads save formats {OLDEST_READABLE} to {CURRENT}). It cannot be loaded and will not be converted. Its backups were saved in that format too. Open it with the build that saved it.";
    }
}
