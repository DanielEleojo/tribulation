// Pure-C# audio shaping math — no UnityEngine dependency, unit-testable.
// Ported from scripts/music.gd _process() logic.
// MonoBehaviour (Music.cs) owns the lerp smoothing + AudioSource writes.

using System;

namespace Tribulation.Core
{
    public static class AudioShaping
    {
        /// <summary>
        /// Target pitch for the music track.
        /// Base: lerp(0.95, 1.12, speedFraction). Tribulation adds +0.08.
        /// Mirrors music.gd _process pitch calculation.
        /// </summary>
        public static float TargetPitch(float speedFraction, bool inTribulation)
        {
            float pitch = 0.95f + (1.12f - 0.95f) * Math.Max(0f, Math.Min(1f, speedFraction));
            if (inTribulation) pitch += 0.08f;
            return pitch;
        }

        /// <summary>
        /// Target volume in dB. Dead → -24; tribulation → -3; normal → -9.
        /// Dead wins over tribulation (matches music.gd if/elif order).
        /// </summary>
        public static float TargetVolumeDb(bool isDead, bool inTribulation)
        {
            if (isDead) return -24f;
            if (inTribulation) return -3f;
            return -9f;
        }

        /// <summary>
        /// Convert dB to linear AudioSource volume.
        /// note: AudioSource.volume is linear — no AudioMixer needed.
        /// </summary>
        public static float DbToLinear(float db)
            => (float)Math.Pow(10.0, db / 20.0);
    }
}
