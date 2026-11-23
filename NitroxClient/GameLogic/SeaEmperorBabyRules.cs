using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     The parts of <see cref="SeaEmperorBabies" /> that do not need the game.
/// </summary>
public static class SeaEmperorBabyRules
{
    /// <summary>
    ///     SeaEmperorBabiesSpawner lets each baby that appears outside the prison drop this many cure balls.
    /// </summary>
    public const int OUTSIDE_DROP_LIMIT = 5;

    /// <summary>
    ///     The id of the baby a spawner makes at a spawn point. Every game finds the spawner in the same place, so every
    ///     player names the same baby the same way: a baby made twice (by a player who carries on after the first one left)
    ///     is the same entity.
    /// </summary>
    public static NitroxId GetBabyId(float spawnerX, float spawnerY, float spawnerZ, int spawnPointIndex)
    {
        return MakeId(spawnerX, spawnerY, spawnerZ, $"baby{spawnPointIndex}");
    }

    /// <summary>
    ///     The id of the cure ball a spawner drops next to the baby of a spawn point.
    /// </summary>
    public static NitroxId GetCureBallId(float spawnerX, float spawnerY, float spawnerZ, int spawnPointIndex)
    {
        return MakeId(spawnerX, spawnerY, spawnerZ, $"ball{spawnPointIndex}");
    }

    /// <summary>
    ///     What the player who makes a spawner's babies holds the lock on, so that only one player makes them at a time.
    /// </summary>
    public static NitroxId GetClaimId(float spawnerX, float spawnerY, float spawnerZ)
    {
        return MakeId(spawnerX, spawnerY, spawnerZ, "claim");
    }

    /// <summary>
    ///     Whether a baby with this drop count and limit still drops cure balls (DropEnzymes.Update disables itself at the limit).
    /// </summary>
    public static bool StillDrops(int dropCount, int dropLimit)
    {
        return dropLimit <= 0 || dropCount < dropLimit;
    }

    private static NitroxId MakeId(float x, float y, float z, string what)
    {
        // Rounded: the same spawner in every game, whatever tiny float difference its position has
        string key = string.Join(":", "SeaEmperorBabiesSpawner", Rounded(x), Rounded(y), Rounded(z), what);
        using MD5 md5 = MD5.Create();
        return new NitroxId(new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(key))));
    }

    private static string Rounded(float value)
    {
        // Invariant: some cultures write the minus sign differently
        return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
    }
}
