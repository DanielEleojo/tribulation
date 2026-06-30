// TDD tests for Telegraph (pure-C# core, no UnityEngine).
// Vertical slices: each test was written RED first, then Telegraph was green'd.

using NUnit.Framework;
using Tribulation.Core;

namespace Tribulation.Tests.EditMode
{
    // ─────────────────────────────────────────────────────────────────────────
    // Slice 1 — Resolve(Block) → Low / Jump / Amber / "Earth-Splitting Sweep"
    // RED : Telegraph doesn't exist yet.
    // GREEN: Telegraph.Resolve() maps HazardKind.Block to its descriptor.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class TelegraphResolveTests
    {
        [Test]
        public void Resolve_Block_ReturnsLowJumpAmberEarthSplittingSweep()
        {
            var info = Telegraph.Resolve(HazardKind.Block);
            Assert.AreEqual(AttackPlane.Low,             info.Plane,       "Block plane");
            Assert.AreEqual(DodgeInput.Jump,             info.Input,       "Block input");
            Assert.AreEqual(TelegraphColor.Amber,        info.Color,       "Block color");
            Assert.AreEqual("Earth-Splitting Sweep",     info.DisplayName, "Block name");
            Assert.AreEqual("earth_splitting_sweep",     info.TechniqueId, "Block id");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Slice 2 — Resolve(Bar) → High / Slide / Cyan / "Heaven-Cleaving Slash"
        // RED : switch falls through to throw.
        // GREEN: add Bar arm to switch.
        // ─────────────────────────────────────────────────────────────────────────
        [Test]
        public void Resolve_Bar_ReturnsHighSlideCyanHeavenCleavingSlash()
        {
            var info = Telegraph.Resolve(HazardKind.Bar);
            Assert.AreEqual(AttackPlane.High,            info.Plane,       "Bar plane");
            Assert.AreEqual(DodgeInput.Slide,            info.Input,       "Bar input");
            Assert.AreEqual(TelegraphColor.Cyan,         info.Color,       "Bar color");
            Assert.AreEqual("Heaven-Cleaving Slash",     info.DisplayName, "Bar name");
            Assert.AreEqual("heaven_cleaving_slash",     info.TechniqueId, "Bar id");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Slice 3 — Resolve(Enemy) → Destructible / Slash / White / "Blocking Disciple"
        // RED : switch throws for Enemy.
        // GREEN: add Enemy arm to switch.
        // ─────────────────────────────────────────────────────────────────────────
        [Test]
        public void Resolve_Enemy_ReturnsDestructibleSlashWhiteBlockingDisciple()
        {
            var info = Telegraph.Resolve(HazardKind.Enemy);
            Assert.AreEqual(AttackPlane.Destructible,    info.Plane,       "Enemy plane");
            Assert.AreEqual(DodgeInput.Slash,            info.Input,       "Enemy input");
            Assert.AreEqual(TelegraphColor.White,        info.Color,       "Enemy color");
            Assert.AreEqual("Blocking Disciple",         info.DisplayName, "Enemy name");
            Assert.AreEqual("blocking_disciple",         info.TechniqueId, "Enemy id");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 4 — ShouldAnnounce: true first time, false second time
    // RED : ShouldAnnounce doesn't exist on Telegraph instance yet.
    // GREEN: HashSet<string> _seen tracks seen ids.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class TelegraphAnnounceTests
    {
        [Test]
        public void ShouldAnnounce_FirstTime_ReturnsTrue_SecondTime_ReturnsFalse()
        {
            var t = new Telegraph();
            Assert.IsTrue(t.ShouldAnnounce("earth_splitting_sweep"),  "first call must be true");
            Assert.IsFalse(t.ShouldAnnounce("earth_splitting_sweep"), "second call must be false");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Slice 5 — Seeing one technique does not mark a different one as seen
        // RED : would catch a naive "mark all" bug.
        // GREEN: HashSet key is per-id, so this passes with the existing impl.
        // ─────────────────────────────────────────────────────────────────────────
        [Test]
        public void ShouldAnnounce_SeenOneId_DoesNotAffectDifferentId()
        {
            var t = new Telegraph();
            t.ShouldAnnounce("earth_splitting_sweep"); // mark this one seen
            Assert.IsTrue(t.ShouldAnnounce("heaven_cleaving_slash"), "different id must still be true");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Slice 6 — LoadSeen preloads ids; preloaded id does not announce, unseen does
        // RED : LoadSeen doesn't exist yet.
        // GREEN: LoadSeen adds ids to _seen HashSet.
        // ─────────────────────────────────────────────────────────────────────────
        [Test]
        public void LoadSeen_PreloadedId_ShouldAnnounce_ReturnsFalse()
        {
            var t = new Telegraph();
            t.LoadSeen(new[] { "earth_splitting_sweep" });
            Assert.IsFalse(t.ShouldAnnounce("earth_splitting_sweep"), "preloaded id must not announce");
        }

        [Test]
        public void LoadSeen_UnseenId_ShouldAnnounce_ReturnsTrue()
        {
            var t = new Telegraph();
            t.LoadSeen(new[] { "earth_splitting_sweep" });
            Assert.IsTrue(t.ShouldAnnounce("heaven_cleaving_slash"), "unseen id must still announce");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 7 — Plane→color/input fixed mapping covers all 4 planes
    // RED : PlaneColor() / PlaneInput() don't exist yet.
    // GREEN: static switch tables in Telegraph.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class TelegraphPlaneMappingTests
    {
        [Test]
        public void Low_MapsTo_Amber_And_Jump()
        {
            Assert.AreEqual(TelegraphColor.Amber, Telegraph.PlaneColor(AttackPlane.Low));
            Assert.AreEqual(DodgeInput.Jump,      Telegraph.PlaneInput(AttackPlane.Low));
        }

        [Test]
        public void High_MapsTo_Cyan_And_Slide()
        {
            Assert.AreEqual(TelegraphColor.Cyan,  Telegraph.PlaneColor(AttackPlane.High));
            Assert.AreEqual(DodgeInput.Slide,     Telegraph.PlaneInput(AttackPlane.High));
        }

        [Test]
        public void Lane_MapsTo_Red_And_Lane()
        {
            Assert.AreEqual(TelegraphColor.Red,   Telegraph.PlaneColor(AttackPlane.Lane));
            Assert.AreEqual(DodgeInput.Lane,      Telegraph.PlaneInput(AttackPlane.Lane));
        }

        [Test]
        public void Destructible_MapsTo_White_And_Slash()
        {
            Assert.AreEqual(TelegraphColor.White, Telegraph.PlaneColor(AttackPlane.Destructible));
            Assert.AreEqual(DodgeInput.Slash,     Telegraph.PlaneInput(AttackPlane.Destructible));
        }
    }
}
