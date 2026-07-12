using System;

namespace NitroxClient.GameLogic;

public static class MapRoomUpgradeEffects
{
    public const float BASE_RANGE = 300f;
    public const float RANGE_PER_MODULE = 50f;
    public const float MAX_RANGE = 500f;
    public const float BASE_INTERVAL = 14f;
    public const float INTERVAL_REDUCTION_PER_MODULE = 3f;
    public const float MIN_INTERVAL = 1f;

    public static float ScanRange(int rangeModules) => Math.Min(MAX_RANGE, BASE_RANGE + Math.Max(0, rangeModules) * RANGE_PER_MODULE);

    public static float ScanInterval(int speedModules) => Math.Max(MIN_INTERVAL, BASE_INTERVAL - Math.Max(0, speedModules) * INTERVAL_REDUCTION_PER_MODULE);
}
