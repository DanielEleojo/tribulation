// RiggedCharacter.cs — drives the rigged "Solider_Fist" humanoid prefab for Tribulation.
// Attach to the player root (Bootstrap does this). Loads the prefab from Resources,
// normalises its scale/grounding to fit the CharacterController, and drives Animator
// state via CrossFade. Falls back to InkCultivator if the prefab is missing.
//
// CONTRACT:
//   • Prefab path : Resources/Char/Solider_Fist  (Humanoid, already has Animator + AC_Fist)
//   • States      : Anim_Fist_Run, Anim_Fist_Attack1, Anim_Fist_Die,
//                   Anim_Fist_Idle1, Anim_Fist_Defense  (drive via CrossFade only)
//   • CharacterController: height 2, center y=1, feet at y=0 on Player root.

using UnityEngine;

[RequireComponent(typeof(PlayerRunner))]
public class RiggedCharacter : MonoBehaviour, IFeelPose
{
    // ── Animator state name constants ─────────────────────────────────────────
    const string STATE_RUN     = "Anim_Fist_Run";
    const string STATE_ATTACK  = "Anim_Fist_Attack1";
    const string STATE_DIE     = "Anim_Fist_Die";
    const string STATE_DEFENSE = "Anim_Fist_Defense";

    // Target character height in world units; matches CharacterController height.
    const float TARGET_HEIGHT = 1.9f;

    // How long the attack animation plays before reverting to run.
    const float SLASH_ANIM_DURATION = 0.5f;

    // How long to hold the death pose before freezing the animator.
    const float DEATH_FREEZE_DELAY = 1.2f;

    // ── Cached references ─────────────────────────────────────────────────────
    PlayerRunner _runner;
    Animator     _animator;

    // ── State tracking ────────────────────────────────────────────────────────
    string _cur = "";            // currently playing state name
    float  _slashTimer;          // > 0 while attack anim should play
    bool   _deathFreezeStarted;  // true once we start the death-freeze countdown
    float  _deathFreezeTimer;    // counts down to freeze; starts at DEATH_FREEZE_DELAY
    bool   _frozen;              // true when animator.speed == 0 (death pose held)

    // ── Feel Pass v1: decaying scale-pop on the model root (Animator never touches root scale) ──
    Transform _model;
    Vector3   _modelBaseScale = Vector3.one;
    float     _pop;
    public void Pop(float strength) { _pop = Mathf.Max(_pop, strength); }

    void LateUpdate()
    {
        if (_model == null || _pop <= 0.0001f) return;
        _pop = Mathf.Lerp(_pop, 0f, Mathf.Clamp01(11f * Time.deltaTime));
        if (_pop < 0.001f) { _pop = 0f; _model.localScale = _modelBaseScale; return; }
        _model.localScale = _modelBaseScale * (1f + _pop);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        try
        {
            // ── Hide placeholder capsule ──────────────────────────────────────
            var vis = transform.Find("Visual");
            if (vis != null)
            {
                var mr = vis.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            // ── Cache PlayerRunner ────────────────────────────────────────────
            _runner = GetComponent<PlayerRunner>();
            if (_runner == null)
            {
                Debug.LogError("[RiggedCharacter] No PlayerRunner on this GameObject. Aborting setup.");
                return;
            }

            // ── Load prefab ───────────────────────────────────────────────────
            var prefab = Resources.Load<GameObject>("Char/Solider_Fist");
            if (prefab == null)
            {
                Debug.LogWarning("[RiggedCharacter] Resources/Char/Solider_Fist not found — falling back to InkCultivator.");
                gameObject.AddComponent<InkCultivator>();
                enabled = false;
                return;
            }

            // ── Instantiate as child at identity local pose ───────────────────
            // 180° Y-rotation: the model's forward is +Z; the player runs toward -Z,
            // so we need the model to face -Z (toward the camera / run direction).
            var instance = Instantiate(prefab, transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _model = instance.transform; // feel: scale-pop target

            // ── Strip colliders from the model (cosmetic only) ────────────────
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            // ── Get Animator and disable root motion ──────────────────────────
            _animator = instance.GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                Debug.LogError("[RiggedCharacter] No Animator found on instantiated model. Aborting setup.");
                Destroy(instance);
                gameObject.AddComponent<InkCultivator>();
                enabled = false;
                return;
            }
            _animator.applyRootMotion = false; // CRITICAL: prevents fighting CharacterController

            // Override the prefab's demo controller (AC_Fist auto-cycles attacks) with our
            // transition-free controller so CrossFade fully owns the state — no auto-advance.
            var rc = Resources.Load<RuntimeAnimatorController>("Anim/FistRunner");
            if (rc != null) _animator.runtimeAnimatorController = rc;

            // ── Normalize scale + grounding ───────────────────────────────────
            // Force Unity to push the skinned mesh into world-space so bounds are valid.
            instance.transform.localScale = Vector3.one;

            // We must activate the object for bounds to be computed on SkinnedMeshRenderers.
            // It is already active (Instantiate activates by default).
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                // Encapsulate all renderer bounds (world space).
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                float modelHeight = combined.size.y;
                if (modelHeight > 0.001f)
                {
                    // Scale uniformly so height == TARGET_HEIGHT.
                    float scale = TARGET_HEIGHT / modelHeight;
                    instance.transform.localScale = new Vector3(scale, scale, scale);

                    // Re-sample bounds after scaling (bounds in world space; convert feet to local).
                    // After scaling, bounds.min.y is the lowest point in world space.
                    // We want the feet to sit at Player root y=0.
                    // The instance's world position.y is 0 (same as player root).
                    // New feet world y = combined.min.y * scale (bounds scale with transform).
                    // Offset so feet land at y=0: localPosition.y = -combined.min.y * scale
                    // But combined.min.y was measured before rescaling, so:
                    //   new_feet_world_y = combined.min.y * scale
                    // We want feet at world y=0, so:
                    //   instance.world_y + new_feet_offset = 0
                    //   localPosition.y = -combined.min.y * scale (since parent is player root at y~0.2 in world, but we work in local)
                    // More precisely: after setting localScale, the world bounds.min.y will be
                    //   playerRoot.position.y + (combined.min.y - playerRoot.position.y) * scale ...
                    // Simplest robust approach: sample bounds again post-scale.
                    // Force a pose update by calling Update on the animator — not reliable without a frame.
                    // Instead, use the pre-scale ratio directly:
                    //   local feet offset = combined.min.y (local space of instance before scale, but we set scale=1 first so world==local for instance).
                    // Because localScale was Vector3.one before, combined.min.y is in world space == instance local space offset from player root.
                    // After applying 'scale', feet local-Y = combined.min.y * scale (relative to player root).
                    // To place feet at localY=0: shift instance down by that amount.
                    float feetOffsetBeforeScale = combined.min.y - transform.position.y; // local to player root at scale=1
                    float feetAfterScale = feetOffsetBeforeScale * scale;
                    instance.transform.localPosition = new Vector3(0f, -feetAfterScale, 0f);
                }
                else
                {
                    Debug.LogWarning("[RiggedCharacter] SkinnedMesh bounds height is zero — skipping scale normalization.");
                }
            }
            else
            {
                Debug.LogWarning("[RiggedCharacter] No SkinnedMeshRenderers found — skipping scale normalization.");
            }

            // Capture the final fitted scale as the base the feel-pop multiplies around.
            _modelBaseScale = instance.transform.localScale;

            // ── Start in Run state ────────────────────────────────────────────
            CrossTo(STATE_RUN, 0f);

            // ── Subscribe to slash event ──────────────────────────────────────
            _runner.Slashed += OnSlashed;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[RiggedCharacter] Setup failed — falling back to InkCultivator. " + ex);
            try
            {
                gameObject.AddComponent<InkCultivator>();
            }
            catch (System.Exception ex2)
            {
                Debug.LogError("[RiggedCharacter] InkCultivator fallback also failed. " + ex2);
            }
            enabled = false;
        }
    }

    void OnDestroy()
    {
        if (_runner != null)
            _runner.Slashed -= OnSlashed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update — state selection and death-freeze management
    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (_animator == null || _runner == null) return;

        // ── Tick slash timer ──────────────────────────────────────────────────
        if (_slashTimer > 0f)
            _slashTimer -= Time.deltaTime;

        // ── Revival: was frozen/dead but runner restarted ─────────────────────
        if (_frozen && !_runner.IsDead)
        {
            _frozen = false;
            _deathFreezeStarted = false;
            _deathFreezeTimer = 0f;
            _animator.speed = 1f;
            CrossTo(STATE_RUN, 0.12f);
            return;
        }

        // ── Death-freeze countdown ────────────────────────────────────────────
        if (_deathFreezeStarted && !_frozen)
        {
            _deathFreezeTimer -= Time.deltaTime;
            if (_deathFreezeTimer <= 0f)
            {
                _frozen = true;
                _animator.speed = 0f;
            }
        }

        if (_frozen) return; // animator is frozen on death pose; nothing more to do

        // ── State selection ───────────────────────────────────────────────────
        string desired;

        if (_runner.IsDead)
        {
            desired = STATE_DIE;
        }
        else if (_slashTimer > 0f)
        {
            desired = STATE_ATTACK;
        }
        else if (_runner.IsSliding)
        {
            desired = STATE_DEFENSE;
        }
        else
        {
            // Run for everything else including airborne (no jump clip in v1).
            desired = STATE_RUN;
        }

        // ── CrossFade only on change ──────────────────────────────────────────
        if (desired != _cur)
        {
            float blend = desired == STATE_DIE ? 0.12f : 0.12f;
            CrossTo(desired, blend);

            // Start death-freeze countdown when entering die state.
            if (desired == STATE_DIE && !_deathFreezeStarted)
            {
                _deathFreezeStarted = true;
                _deathFreezeTimer = DEATH_FREEZE_DELAY;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnSlashed — fired by PlayerRunner.Slashed event
    // ─────────────────────────────────────────────────────────────────────────
    void OnSlashed()
    {
        _slashTimer = SLASH_ANIM_DURATION;
        if (_animator != null)
            _animator.CrossFade(STATE_ATTACK, 0.06f);
        _cur = STATE_ATTACK;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    void CrossTo(string state, float blend)
    {
        if (_animator == null) return;
        _animator.CrossFade(state, blend);
        _cur = state;
    }
}
