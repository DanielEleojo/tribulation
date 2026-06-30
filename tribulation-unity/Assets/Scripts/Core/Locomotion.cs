// Pure-C# locomotion helpers for Qi-Leap (double jump) and Cloud-Tread (glide).
// No UnityEngine dependency — fully unit-testable via the coretest harness.
// Ported from player.gd _max_air_jumps() (line 492), _can_glide() (497),
// _glide_held() (503), and the gravity line (439-440).

namespace Tribulation.Core
{
    public static class Locomotion
    {
        /// <summary>
        /// Number of extra mid-air jumps available.
        /// Qi-Leap (Foundation Establishment+): 1 when doublejump ability is unlocked.
        /// Mirrors player.gd _max_air_jumps() (line 492).
        /// </summary>
        public static int MaxAirJumps(bool hasDoubleJump) => hasDoubleJump ? 1 : 0;

        /// <summary>
        /// Effective gravity for this frame.
        /// Cloud-Tread (Nascent Soul+): multiplies gravity by 0.22 while falling and
        /// the player holds jump — otherwise returns baseGravity unchanged.
        /// Mirrors player.gd lines 438-440.
        /// </summary>
        public static float GlideGravity(float baseGravity, bool grounded, float vy, bool canGlide, bool glideHeld)
        {
            if (!grounded && vy < 0f && canGlide && glideHeld)
                return baseGravity * 0.22f;
            return baseGravity;
        }
    }
}
