// Pure-C# coach-mark tutorial state. No UnityEngine dependency.
// Ported from hud.gd + game.gd _pick_lesson / _on_lesson_learned.
//
// Lesson ids: "lane", "jump", "slide", "slash"
// Lesson-selection rule (PickLesson): among active hazards ahead of the player within
//   (COACH_NEAR, COACH_FAR), pick the nearest whose lesson is unlearned.
//   Skip "slash" if !hasSlash. Else fall back to "lane" early in the run. Else "".

using System;
using System.Collections.Generic;

namespace Tribulation.Core
{
    public class TutorialState
    {
        // ── Coach-mark z-window (Godot originals) ────────────────────────────
        public const float COACH_NEAR = 6.0f;
        public const float COACH_FAR  = 46.0f;

        // ── Lesson id constants ────────────────────────────────────────────────
        public const string LESSON_LANE  = "lane";
        public const string LESSON_JUMP  = "jump";
        public const string LESSON_SLIDE = "slide";
        public const string LESSON_SLASH = "slash";

        // ── Lesson-for-hazard-kind mapping ────────────────────────────────────
        public static string LessonForKind(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Block:  return LESSON_JUMP;
                case HazardKind.Bar:    return LESSON_SLIDE;
                case HazardKind.Enemy:  return LESSON_SLASH;
                default: return "";
            }
        }

        // ── Instance state ─────────────────────────────────────────────────────
        readonly HashSet<string> _learned = new HashSet<string>();

        public bool IsLearned(string id) => _learned.Contains(id);

        public void Learn(string id)
        {
            _learned.Add(id);
        }

        // ── Persistence ────────────────────────────────────────────────────────
        public IReadOnlyCollection<string> LearnedLessons => _learned;

        public void LoadLearned(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids) _learned.Add(id);
        }

        // ── Pure static selector (mirrors hud.gd _pick_lesson) ───────────────
        /// <summary>
        /// Returns the lesson id to show, or "" if nothing should be shown.
        /// playerZ      — player world Z
        /// activeHazards — (lesson id string, hazard world Z) pairs from Spawner._live
        /// hasSlash     — GameCore.HasAbility("slash")
        /// runDistance  — distance the player has traveled this run (li)
        /// isLearned    — predicate (maps to TutorialState.IsLearned)
        /// </summary>
        public static string PickLesson(
            float playerZ,
            IEnumerable<(string lesson, float z)> activeHazards,
            bool hasSlash,
            float runDistance,
            Func<string, bool> isLearned)
        {
            // Find nearest unlearned teachable hazard within (COACH_NEAR, COACH_FAR).
            // "Ahead" = hazard is in front of player, i.e. hazardZ < playerZ (forward is -Z).
            // Distance d = playerZ - hazardZ; teachable when COACH_NEAR < d < COACH_FAR.
            float bestDist = float.MaxValue;
            string bestLesson = "";

            foreach (var (lesson, hazardZ) in activeHazards)
            {
                if (string.IsNullOrEmpty(lesson)) continue;

                // Skip "slash" if the player doesn't have slash ability yet.
                if (lesson == LESSON_SLASH && !hasSlash) continue;

                float d = playerZ - hazardZ; // positive means ahead of player
                if (d <= COACH_NEAR || d >= COACH_FAR) continue; // outside window

                if (isLearned(lesson)) continue; // already learned

                if (d < bestDist)
                {
                    bestDist   = d;
                    bestLesson = lesson;
                }
            }

            if (bestLesson != "") return bestLesson;

            // Fallback: show "lane" early in the run if unlearned.
            if (runDistance < 140f && !isLearned(LESSON_LANE))
                return LESSON_LANE;

            return "";
        }
    }
}
