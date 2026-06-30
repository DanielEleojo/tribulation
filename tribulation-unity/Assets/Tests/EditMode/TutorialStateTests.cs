// EditMode tests for TutorialState (pure-C# core, no MonoBehaviour).
// TDD slices: PickLesson selection rule, LessonForKind mapping,
//             learned-state, SaveData round-trip.

using System.Collections.Generic;
using NUnit.Framework;
using Tribulation.Core;

namespace Tribulation.Tests.EditMode
{
    [TestFixture]
    public class TutorialStateLessonForKindTests
    {
        [Test]
        public void Block_ReturnsJump()
            => Assert.AreEqual(TutorialState.LESSON_JUMP, TutorialState.LessonForKind(HazardKind.Block));

        [Test]
        public void Bar_ReturnsSlide()
            => Assert.AreEqual(TutorialState.LESSON_SLIDE, TutorialState.LessonForKind(HazardKind.Bar));

        [Test]
        public void Enemy_ReturnsSlash()
            => Assert.AreEqual(TutorialState.LESSON_SLASH, TutorialState.LessonForKind(HazardKind.Enemy));
    }

    [TestFixture]
    public class TutorialStateLearnedTests
    {
        [Test]
        public void IsLearned_FalseByDefault()
        {
            var ts = new TutorialState();
            Assert.IsFalse(ts.IsLearned(TutorialState.LESSON_LANE));
        }

        [Test]
        public void Learn_MarksPermanent()
        {
            var ts = new TutorialState();
            ts.Learn(TutorialState.LESSON_JUMP);
            Assert.IsTrue(ts.IsLearned(TutorialState.LESSON_JUMP));
        }

        [Test]
        public void Learn_DoesNotAffectOtherLessons()
        {
            var ts = new TutorialState();
            ts.Learn(TutorialState.LESSON_JUMP);
            Assert.IsFalse(ts.IsLearned(TutorialState.LESSON_SLIDE));
        }

        [Test]
        public void LoadLearned_RestoresFromList()
        {
            var ts = new TutorialState();
            ts.LoadLearned(new[] { TutorialState.LESSON_LANE, TutorialState.LESSON_SLIDE });
            Assert.IsTrue(ts.IsLearned(TutorialState.LESSON_LANE));
            Assert.IsTrue(ts.IsLearned(TutorialState.LESSON_SLIDE));
            Assert.IsFalse(ts.IsLearned(TutorialState.LESSON_JUMP));
        }

        [Test]
        public void LoadLearned_NullInput_IsNoop()
        {
            var ts = new TutorialState();
            Assert.DoesNotThrow(() => ts.LoadLearned(null));
        }
    }

    [TestFixture]
    public class TutorialStateSaveRoundTripTests
    {
        static BalanceData MinBal() => new BalanceData
        {
            realm_span     = new[] { 999 },
            qi_max         = 100f,
            net_close_rate = 0f,
        };

        [Test]
        public void ToSave_IncludesLearnedLessons()
        {
            var core = new GameCore(MinBal());
            core.Tutorial.Learn(TutorialState.LESSON_JUMP);
            core.Tutorial.Learn(TutorialState.LESSON_SLIDE);

            var save = core.ToSave();
            Assert.IsNotNull(save.learnedLessons);
            Assert.IsTrue(save.learnedLessons.Contains(TutorialState.LESSON_JUMP));
            Assert.IsTrue(save.learnedLessons.Contains(TutorialState.LESSON_SLIDE));
        }

        [Test]
        public void LoadSave_RestoresLearnedLessons()
        {
            var core = new GameCore(MinBal());
            core.Tutorial.Learn(TutorialState.LESSON_SLASH);
            var save = core.ToSave();

            // New core loads the save — should restore learned lessons.
            var core2 = new GameCore(MinBal());
            core2.LoadSave(save);
            Assert.IsTrue(core2.Tutorial.IsLearned(TutorialState.LESSON_SLASH));
            Assert.IsFalse(core2.Tutorial.IsLearned(TutorialState.LESSON_LANE));
        }

        [Test]
        public void LoadSave_NullLearnedLessons_DoesNotCrash()
        {
            var core = new GameCore(MinBal());
            var save = new SaveData { learnedLessons = null };
            Assert.DoesNotThrow(() => core.LoadSave(save));
        }

        [Test]
        public void LoadSave_EmptyLearnedLessons_AllUnlearned()
        {
            var core = new GameCore(MinBal());
            core.Tutorial.Learn(TutorialState.LESSON_LANE);
            // fresh save with empty list
            var save = core.ToSave();
            save.learnedLessons = new List<string>();

            var core2 = new GameCore(MinBal());
            core2.LoadSave(save);
            Assert.IsFalse(core2.Tutorial.IsLearned(TutorialState.LESSON_LANE));
        }
    }

    [TestFixture]
    public class TutorialStatePickLessonTests
    {
        // Helper: build a list of (lesson, z) tuples from parallel arrays.
        static List<(string lesson, float z)> H(params (string, float)[] pairs)
        {
            var list = new List<(string, float)>();
            foreach (var p in pairs) list.Add(p);
            return list;
        }

        // shorthand for isLearned always-false
        static bool Never(string _) => false;

        // ── Window boundary tests ────────────────────────────────────────────

        [Test]
        public void HazardInWindow_PicksItsLesson()
        {
            // playerZ = 100, hazardZ = 75  → d = 25 → inside (6, 46)
            var hazards = H(("jump", 75f));
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("jump", result);
        }

        [Test]
        public void HazardTooClose_Ignored()
        {
            // d = 3 < COACH_NEAR=6 → outside window
            var hazards = H(("jump", 97f)); // playerZ=100, d=3
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("", result);
        }

        [Test]
        public void HazardTooFar_Ignored()
        {
            // d = 50 > COACH_FAR=46 → outside window
            var hazards = H(("jump", 50f)); // playerZ=100, d=50
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("", result);
        }

        [Test]
        public void HazardExactlyAtNear_Excluded()
        {
            // d == COACH_NEAR is NOT in the open interval (COACH_NEAR, COACH_FAR)
            var hazards = H(("jump", 94f)); // d = 6.0 exactly
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("", result);
        }

        [Test]
        public void HazardExactlyAtFar_Excluded()
        {
            // d == COACH_FAR is NOT in the open interval
            var hazards = H(("jump", 54f)); // d = 46.0 exactly
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("", result);
        }

        // ── Nearest-wins ─────────────────────────────────────────────────────

        [Test]
        public void NearestHazardWins()
        {
            // Two hazards in window; nearer one (d=10) wins over farther (d=30)
            var hazards = H(("slide", 90f), ("jump", 70f)); // d=10, d=30 from playerZ=100
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("slide", result);
        }

        // ── Learned-skip ─────────────────────────────────────────────────────

        [Test]
        public void LearnedLesson_Skipped_NextNearest()
        {
            // Nearest "slide" is learned; "jump" further back is the fallback
            var hazards = H(("slide", 90f), ("jump", 70f)); // d=10, d=30
            string result = TutorialState.PickLesson(
                100f, hazards, hasSlash: true, runDistance: 200f,
                isLearned: id => id == "slide");
            Assert.AreEqual("jump", result);
        }

        [Test]
        public void AllHazardLessonsLearned_FallsThrough()
        {
            var hazards = H(("slide", 90f), ("jump", 70f));
            string result = TutorialState.PickLesson(
                100f, hazards, hasSlash: true, runDistance: 200f,
                isLearned: _ => true); // everything learned
            // No hazard, run >= 140 → nothing
            Assert.AreEqual("", result);
        }

        // ── Slash gate ────────────────────────────────────────────────────────

        [Test]
        public void SlashLesson_SkippedWhenNoSlashAbility()
        {
            // Only hazard is an Enemy ("slash"), but hasSlash=false
            var hazards = H(("slash", 80f)); // d=20, in window
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: false, runDistance: 200f, Never);
            Assert.AreEqual("", result);
        }

        [Test]
        public void SlashLesson_ShownWhenHasSlash()
        {
            var hazards = H(("slash", 80f)); // d=20, in window
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual("slash", result);
        }

        // ── Lane fallback ─────────────────────────────────────────────────────

        [Test]
        public void NoHazards_EarlyRun_LaneUnlearned_ShowsLane()
        {
            string result = TutorialState.PickLesson(
                100f, H(), hasSlash: true, runDistance: 50f, Never);
            Assert.AreEqual("lane", result);
        }

        [Test]
        public void NoHazards_EarlyRun_LaneLearned_ShowsNothing()
        {
            string result = TutorialState.PickLesson(
                100f, H(), hasSlash: true, runDistance: 50f,
                isLearned: id => id == "lane");
            Assert.AreEqual("", result);
        }

        [Test]
        public void NoHazards_LateRun_ShowsNothing()
        {
            // runDistance >= 140 → no lane fallback
            string result = TutorialState.PickLesson(
                100f, H(), hasSlash: true, runDistance: 140f, Never);
            Assert.AreEqual("", result);
        }

        [Test]
        public void NoHazards_LateRun_LaneUnlearned_StillNothing()
        {
            string result = TutorialState.PickLesson(
                100f, H(), hasSlash: true, runDistance: 500f, Never);
            Assert.AreEqual("", result);
        }

        // ── HazardKind mapping round-trip ─────────────────────────────────────

        [Test]
        public void LessonForKind_UsedInPickLesson_WorksEndToEnd()
        {
            // Build hazards using LessonForKind, then pick
            var hazards = new List<(string, float)>
            {
                (TutorialState.LessonForKind(HazardKind.Block), 80f), // "jump", d=20
            };
            string result = TutorialState.PickLesson(100f, hazards, hasSlash: true, runDistance: 200f, Never);
            Assert.AreEqual(TutorialState.LESSON_JUMP, result);
        }
    }
}
