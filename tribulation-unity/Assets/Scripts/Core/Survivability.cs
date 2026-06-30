// Pure-C# survivability state machine: Iron-Body shields + Blood-Sprint + Dread Form flag.
// No UnityEngine dependency — fully unit-testable via the coretest harness.
// Ported from player.gd lines 72-81, 417-427, 639-640, 702-711, 714-722, 728.
//
// Issue #8 — Survivability: shields + Blood-Sprint + Dread Form

using System;

namespace Tribulation.Core
{
    public class Survivability
    {
        // ── Constants (player.gd lines 79-81) ─────────────────────────────────
        public const float SHIELD_REGEN_TIME  = 9.0f;
        public const float SPRINT_DECAY       = 4.0f;
        public const float SPRINT_CAP         = 8.0f;
        public const float INVULN_ON_ABSORB   = 1.0f;

        // ── State ──────────────────────────────────────────────────────────────
        public int   Shields      { get; private set; }
        public int   MaxShields   { get; private set; }
        public float SprintBoost  { get; private set; }
        public float InvulnT      { get; private set; }

        // Whether Dread Form is active (Ascension-tier toggle).
        public bool  Dread        { get; private set; }

        // Internal regen timer and per-kill sprint increment.
        float _regenTimer;
        float _sprintPerKill;

        // ── ApplyRealmStats ───────────────────────────────────────────────────
        // Mirrors player.gd apply_realm_stats() (lines 702-711).
        // shieldMax: new cap; if it grew, fill the diff immediately.
        // sprintPerKill: Blood-Sprint per kill increment for this realm.
        public void ApplyRealmStats(int shieldMax, float sprintPerKill)
        {
            if (shieldMax > MaxShields)
                Shields += shieldMax - MaxShields;  // new slot granted filled
            MaxShields = shieldMax;
            Shields = Math.Min(Shields, MaxShields);
            _sprintPerKill = sprintPerKill;
        }

        // ── TryAbsorbHit ──────────────────────────────────────────────────────
        // Mirrors player.gd try_absorb_hit() (lines 714-722).
        // Returns true if the hit was absorbed (no death). false = player dies.
        public bool TryAbsorbHit()
        {
            if (InvulnT > 0f)
                return true;                        // still invulnerable from prior absorb
            if (Shields > 0)
            {
                Shields--;
                InvulnT = INVULN_ON_ABSORB;
                _regenTimer = SHIELD_REGEN_TIME;
                return true;
            }
            return false;
        }

        // ── OnKills ───────────────────────────────────────────────────────────
        // Mirrors player.gd line 640: _sprint_boost = minf(CAP, _sprint_boost + per_kill * killed).
        public void OnKills(int killed)
        {
            SprintBoost = Math.Min(SPRINT_CAP, SprintBoost + _sprintPerKill * killed);
        }

        // ── Tick ──────────────────────────────────────────────────────────────
        // Mirrors player.gd lines 418-427.
        public void Tick(float delta)
        {
            // Invulnerability countdown.
            if (InvulnT > 0f)
                InvulnT = Math.Max(0f, InvulnT - delta);

            // Shield regen: count down while below max; restore one shield per period.
            if (Shields < MaxShields)
            {
                _regenTimer -= delta;
                if (_regenTimer <= 0f)
                {
                    Shields++;
                    _regenTimer = SHIELD_REGEN_TIME;
                }
            }

            // Blood-Sprint decays toward 0.
            if (SprintBoost > 0f)
                SprintBoost = Math.Max(0f, SprintBoost - SPRINT_DECAY * delta);
        }

        // ── GrantShield ───────────────────────────────────────────────────────
        // Iron Aegis consumable bonus charge: +1 shield, allowed to exceed MaxShields
        // (aegis is a one-use absorb usable at any realm). Mirrors player.gd grant_shield().
        public void GrantShield()
        {
            Shields++;
            MaxShields = Math.Max(MaxShields, Shields);
        }

        // ── SetDread ──────────────────────────────────────────────────────────
        // Toggle Dread Form. Called by PlayerRunner when realm enters/exits Ascension.
        // ponytail: visual aura deferred — just the flag here.
        public void SetDread(bool active) => Dread = active;

        // ── Reset ─────────────────────────────────────────────────────────────
        // For ResetRun: keep realm-granted MaxShields/sprintPerKill, refill Shields,
        // clear transient invuln/sprint/regen state. Mirrors a fresh-run in player.gd.
        public void Reset()
        {
            Shields     = MaxShields;   // refill granted shields
            InvulnT     = 0f;
            SprintBoost = 0f;
            _regenTimer = 0f;
            // MaxShields and _sprintPerKill persist — they come from realm stats.
        }
    }
}
