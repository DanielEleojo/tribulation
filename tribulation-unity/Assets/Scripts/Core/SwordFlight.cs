// Pure-C# sword-flight state machine. No UnityEngine dependency — fully unit-testable.
// Ported from player.gd flight consts (lines 52-57), _can_swordfly (511), _enter_flight (516),
// _exit_flight (536), _process_flight vertical logic (555-564).
// PlayerRunner.cs feeds grounded/canFly/held flags/y and reads IsFlying + ClimbVelocity.

using System;

namespace Tribulation.Core
{
    public class SwordFlight
    {
        // ── Consts (player.gd lines 52-57) ──────────────────────────────────────
        public const float MIN_Y     = 2.2f;
        public const float MAX_Y     = 6.0f;
        public const float CLIMB     = 7.0f;
        public const float DURATION  = 8.0f;
        public const float COOLDOWN  = 16.0f;
        public const float FIRST     = 7.0f;

        // ── State ─────────────────────────────────────────────────────────────
        public bool IsFlying { get; private set; }

        float _cooldownLeft = FIRST;   // starts at FIRST; resets to COOLDOWN after exit
        float _flightLeft;             // counts down while IsFlying

        // ── Tick (called every frame by PlayerRunner) ──────────────────────────
        // Mirrors player.gd lines 373-377 and _process_flight lines 580-582.
        // grounded  = CharacterController.isGrounded (NOT flying check — PlayerRunner
        //             feeds true while on the ground, false mid-air / during flight).
        // canFly    = core.HasAbility("swordflight")
        public void Tick(float delta, bool grounded, bool canFly)
        {
            if (IsFlying)
            {
                _flightLeft -= delta;
                if (_flightLeft <= 0f)
                    ExitFlight();
                return;
            }

            // Cooldown only ticks while grounded AND ability unlocked (player.gd line 373).
            if (grounded && canFly)
            {
                _cooldownLeft -= delta;
                if (_cooldownLeft <= 0f)
                    EnterFlight();
            }
        }

        // ── ClimbVelocity (player.gd _process_flight lines 556-563) ───────────
        // Returns the vertical velocity to apply while flying.
        // Call only while IsFlying; safe to call otherwise (returns 0).
        public float ClimbVelocity(float currentY, bool climbHeld, bool diveHeld)
        {
            if (currentY < MIN_Y)
                return CLIMB;                                   // lift onto band floor
            if (climbHeld)
                return currentY < MAX_Y ? CLIMB : 0f;          // climb while below ceiling
            if (diveHeld)
                return currentY > MIN_Y ? -CLIMB : 0f;         // dive while above floor
            return 0f;
        }

        // ── Reset (called by PlayerRunner.ResetRun) ────────────────────────────
        public void Reset()
        {
            IsFlying      = false;
            _cooldownLeft = FIRST;
            _flightLeft   = 0f;
        }

        // ── Internal ──────────────────────────────────────────────────────────
        void EnterFlight()
        {
            IsFlying    = true;
            _flightLeft = DURATION;
            // ponytail: end slide signal — PlayerRunner checks IsFlying and calls EndSlide if needed
            // ponytail: sword-mount VFX — deferred to visual polish
            // ponytail: SFX — deferred to audio issue
        }

        void ExitFlight()
        {
            IsFlying      = false;
            _cooldownLeft = COOLDOWN;
            // ponytail: sword-mount teardown VFX — deferred to visual polish
            // gravity resumes next PlayerRunner frame → natural descent
        }
    }
}
