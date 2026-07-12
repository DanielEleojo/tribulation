// Pure-C# near-miss classifier. No UnityEngine dependency.
// Used by Spawner's near-miss scan: an Enemy hazard that passes the player untouched,
// by a whisker, is a skillful dodge worth rewarding.
namespace Tribulation.Core
{
    /// <summary>Pure classifier for near-miss detection. gap = |player.x − hazard.x|.
    /// A near miss is the tight band just OUTSIDE the collision envelope: the player
    /// cleared it, but barely.</summary>
    public static class NearMiss
    {
        public static bool IsNearMiss(float lateralGap, float halfWidthSum, float band)
            => lateralGap > halfWidthSum && lateralGap <= halfWidthSum + band;
    }
}
