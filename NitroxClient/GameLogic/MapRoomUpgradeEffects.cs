using System;

namespace NitroxClient.GameLogic;

public static class MapRoomUpgradeEffects
{
    internal const float BASE_RANGE = 300f;
    internal const float RANGE_PER_MODULE = 50f;
    internal const float MAX_RANGE = 500f;
    internal const float BASE_INTERVAL = 14f;
    internal const float INTERVAL_REDUCTION_PER_MODULE = 3f;
    internal const float MIN_INTERVAL = 1f;

    public static float ScanRange(int rangeModules) => Math.Min(MAX_RANGE, BASE_RANGE + Math.Max(0, rangeModules) * RANGE_PER_MODULE);

    public static float ScanInterval(int speedModules) => Math.Max(MIN_INTERVAL, BASE_INTERVAL - Math.Max(0, speedModules) * INTERVAL_REDUCTION_PER_MODULE);
}
