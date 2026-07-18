namespace ScannerRoom.NetworkImpairmentProxy;

/// <summary>
/// SplitMix64 with rejection sampling. Its output is stable across .NET runtime versions.
/// </summary>
internal sealed class DeterministicRandom(ulong seed)
{
    private ulong state = seed;

    public int Next(int exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveUpperBound);

        uint bound = (uint)exclusiveUpperBound;
        uint threshold = unchecked(0u - bound) % bound;
        while (true)
        {
            uint value = (uint)NextUInt64();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }

    private ulong NextUInt64()
    {
        ulong value = state += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
