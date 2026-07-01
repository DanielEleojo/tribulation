// EnemyBehavior.cs — active enemy AI for the Tribulation runner.
// Attached to the enemy hazard GameObject by Spawner.SpawnEnemy (pool-safe via Activate()).
//
// Two modes:
//   CHARGER  — rushes the player in +Z at CHARGE_SPEED once within ACTIVATE_DIST. Lane stays fixed.
//   LUNGER   — same +Z rush (slightly slower) AND swerves into the player's lane when close.
//              The player can dodge by switching lanes AFTER the lunge telegraph fires.
//
// Mode is re-selected every spawn via Activate(); all state is instance fields, no statics.

using UnityEngine;

public class EnemyBehavior : MonoBehaviour
{
    // ── Tunables ─────────────────────────────────────────────────────────────────
    const float ACTIVATE_DIST   = 30f;   // gap (z) at which the enemy starts rushing
    const float CHARGE_SPEED    = 7f;    // CHARGER rush speed in +Z (units/s)
    const float LUNGER_SPEED    = 5f;    // LUNGER forward rush speed (slightly slower for read time)
    const float LUNGE_TRIGGER   = 16f;   // gap at which the LUNGER snaps its target lane
    const float LUNGE_TIME      = 0.35f; // seconds to lerp from spawn lane X to target X
    const float LUNGE_LATERAL   = 18f;   // max lateral speed (units/s) used as SmoothDamp maxSpeed
    const float DIE_ANIM_SECS   = 1.1f;  // despawn delay after Kill() — matches Die clip length

    // ── Mode enum ────────────────────────────────────────────────────────────────
    enum Mode { Charger, Lunger }

    // ── Per-spawn state (reset by Activate) ──────────────────────────────────────
    Mode      _mode;
    Transform _player;
    float     _spawnX;      // lane X captured at Activate time

    bool  _lungeTriggered;  // has the LUNGER snapped its target lane yet?
    float _lungeTargetX;    // player's X captured at lunge trigger time (once)
    float _lungeElapsed;    // how many seconds into the lateral lerp
    float _lateVelX;        // SmoothDamp velocity ref for lateral movement

    // ── Death state ──────────────────────────────────────────────────────────────
    bool  _dying;
    float _dieTimer;
    Animator _anim;

    // ── Activate (called fresh on every spawn/pool-reuse) ────────────────────────
    /// <summary>
    /// Called by Spawner.SpawnEnemy after the enemy position is set.
    /// Resets all per-spawn state and picks the mode for this life.
    /// </summary>
    public void Activate(Transform player, int realm)
    {
        // Reset all per-spawn fields before re-initialising.
        _lungeTriggered = false;
        _lungeTargetX   = 0f;
        _lungeElapsed   = 0f;
        _lateVelX       = 0f;

        // Reset death state.
        _dying    = false;
        _dieTimer = 0f;

        _player  = player;
        _spawnX  = transform.position.x;

        // Re-enable collider in case a prior life disabled it.
        var col = GetComponent<BoxCollider>();
        if (col != null) col.enabled = true;

        // Re-enable Foe marker so TrySlash can find this enemy again.
        var foe = GetComponent<Foe>();
        if (foe != null) foe.enabled = true;

        // Cache animator and return to Run state (avoids death-pose on pool reuse).
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null) _anim.CrossFade("Run", 0.1f);

        // Mode selection:
        //   realm < 2  → always CHARGER
        //   realm >= 2 → LUNGER with probability clamped to [0.3 .. 1.0], else CHARGER
        if (realm < 2)
        {
            _mode = Mode.Charger;
        }
        else
        {
            float lungerChance = Mathf.Clamp01(0.3f + 0.12f * (realm - 2));
            _mode = (UnityEngine.Random.value < lungerChance) ? Mode.Lunger : Mode.Charger;
        }
    }

    // ── Kill (called by PlayerRunner.TrySlash) ───────────────────────────────────
    /// <summary>
    /// Begins the death sequence: disables the collider + Foe, plays Die anim,
    /// then SetActive(false) after DIE_ANIM_SECS to return to pool.
    /// </summary>
    public void Kill()
    {
        if (_dying) return;
        _dying    = true;
        _dieTimer = DIE_ANIM_SECS;

        // Disable collider immediately — can't hit player or be re-slashed.
        var col = GetComponent<BoxCollider>();
        if (col != null) col.enabled = false;

        // Disable Foe so TrySlash's iteration skips this enemy.
        var foe = GetComponent<Foe>();
        if (foe != null) foe.enabled = false;

        // Play death animation.
        if (_anim != null) _anim.CrossFade("Die", 0.05f);
    }

    // ── Update ────────────────────────────────────────────────────────────────────
    void Update()
    {
        // Death countdown — runs even while dying (no movement).
        if (_dying)
        {
            _dieTimer -= Time.deltaTime;
            if (_dieTimer <= 0f && gameObject.activeSelf)
                gameObject.SetActive(false); // return to pool
            return; // no movement while dying
        }

        // Guard: inert if no player, player is dead, or the enemy has already passed the player.
        if (_player == null) return;

        var pr = _player.GetComponent<PlayerRunner>();
        if (pr != null && pr.IsDead) return;

        float gap = _player.position.z - transform.position.z;
        if (gap <= 0f) return; // enemy is behind the player — stop moving

        // ── Choose per-mode behaviour ──────────────────────────────────────────
        switch (_mode)
        {
            case Mode.Charger:
                UpdateCharger(gap);
                break;
            case Mode.Lunger:
                UpdateLunger(gap);
                break;
        }
    }

    // ── CHARGER ───────────────────────────────────────────────────────────────────
    // Once within ACTIVATE_DIST, rush straight in +Z at CHARGE_SPEED.
    // X stays fixed at _spawnX; the player must switch lanes.
    void UpdateCharger(float gap)
    {
        if (gap >= ACTIVATE_DIST) return; // not close enough yet

        // Move in +Z to close the gap smoothly (no teleport — scaled by deltaTime).
        Vector3 pos = transform.position;
        pos.z += CHARGE_SPEED * Time.deltaTime;
        transform.position = pos;
    }

    // ── LUNGER ────────────────────────────────────────────────────────────────────
    // Same +Z rush as CHARGER (but slightly slower), PLUS at LUNGE_TRIGGER distance
    // it captures the player's current lane X and lerps sideways to that X over LUNGE_TIME.
    // The capture-once design lets the player dodge by switching lanes after the telegraph fires.
    void UpdateLunger(float gap)
    {
        // Forward rush — starts at ACTIVATE_DIST like the charger.
        if (gap < ACTIVATE_DIST)
        {
            Vector3 pos = transform.position;
            pos.z += LUNGER_SPEED * Time.deltaTime;
            transform.position = pos;
        }

        // Lateral lunge — triggered once when gap drops below LUNGE_TRIGGER.
        if (!_lungeTriggered && gap < LUNGE_TRIGGER)
        {
            _lungeTriggered = true;
            _lungeTargetX   = _player.position.x; // snap player lane at trigger time
            _lungeElapsed   = 0f;
            _lateVelX       = 0f;
        }

        if (_lungeTriggered && _lungeElapsed < LUNGE_TIME)
        {
            _lungeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_lungeElapsed / LUNGE_TIME);

            // Smooth lateral lerp from spawn lane X toward the captured target X.
            // Using SmoothDamp for a natural ease-in ease-out feel.
            float currentX = transform.position.x;
            float newX = Mathf.SmoothDamp(
                currentX,
                _lungeTargetX,
                ref _lateVelX,
                LUNGE_TIME * 0.4f,   // smoothTime ≈ 40% of total lunge duration for quick snap
                LUNGE_LATERAL,       // maxSpeed cap
                Time.deltaTime);

            // Also snap fully at t=1 to avoid floating-point drift.
            if (t >= 1f) newX = _lungeTargetX;

            Vector3 pos = transform.position;
            pos.x = newX;
            transform.position = pos;
        }
    }
}
