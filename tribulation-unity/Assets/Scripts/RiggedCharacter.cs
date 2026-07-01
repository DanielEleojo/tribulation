// RiggedCharacter.cs — drives the rigged "Solider_Fist" humanoid prefab for Tribulation.
// Attach to the player root (Bootstrap does this). Loads the prefab from Resources,
// normalises its scale/grounding to fit the CharacterController, and drives Animator
// state via CrossFade. Falls back to InkCultivator if the prefab is missing.
//
// CONTRACT:
//   • Prefab path : Resources/Char/Solider_Fist  (Humanoid, already has Animator + AC_Fist)
//   • States      : Anim_Fist_Run, Anim_Fist_Attack1, Anim_Fist_Die,
//                   Anim_Fist_Idle1, Anim_Fist_Defense,
//                   Anim_Fist_Jump (Idle2 stand-in), Anim_Fist_Fall (Damage stand-in)
//                   (drive via CrossFade only)
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
    const string STATE_JUMP    = "Anim_Fist_Jump";   // stand-in: Idle2 (braced pose)
    const string STATE_FALL    = "Anim_Fist_Fall";   // stand-in: Damage (off-balance recoil)

    // Target character height in world units; matches CharacterController height.
    const float TARGET_HEIGHT = 1.9f;

    // How long the attack animation plays before reverting to run.
    const float SLASH_ANIM_DURATION = 0.5f;

    // How long to hold the death pose before freezing the animator.
    const float DEATH_FREEZE_DELAY = 1.2f;

    // ── Explicit fallback flag ────────────────────────────────────────────────
    // Set true (in Inspector or via Bootstrap) to skip the rigged setup and use
    // InkCultivator instead. Auto-fallback (prefab missing / exception) still applies.
    [SerializeField] public bool forceFallback = false;

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

    // ── Procedural slide pose ─────────────────────────────────────────────────
    // The Fist clip set has no slide/crouch clip, so we fake a feet-first slide by
    // reclining the model back + dropping it low while IsSliding. Reads as a slide
    // regardless of the underlying clip.
    Vector3    _modelBasePos;
    Quaternion _modelBaseRot = Quaternion.identity;
    float      _slideBlend;                 // 0 = standing, 1 = full slide recline
    const float SLIDE_PITCH      = -74f;    // degrees reclined back about local X
    const float SLIDE_DROP       = 0.28f;   // world units the body drops
    const float SLIDE_BLEND_TIME = 0.10f;   // seconds to blend in/out

    void LateUpdate()
    {
        if (_model == null) return;

        // Blend the slide recline in/out from the runner's slide state.
        float slideTarget = (_runner != null && _runner.IsSliding && !_runner.IsDead) ? 1f : 0f;
        _slideBlend = Mathf.MoveTowards(_slideBlend, slideTarget,
                                        Time.deltaTime / Mathf.Max(0.0001f, SLIDE_BLEND_TIME));

        // Decay the feel pop (scale) independently.
        if (_pop > 0.0001f) _pop = Mathf.Lerp(_pop, 0f, Mathf.Clamp01(11f * Time.deltaTime));
        if (_pop < 0.001f)  _pop = 0f;

        // Nothing active → hold the grounded base pose (cheap early-out).
        if (_slideBlend <= 0.0001f && _pop <= 0.0001f)
        {
            _model.localScale    = _modelBaseScale;
            _model.localRotation = _modelBaseRot;
            _model.localPosition = _modelBasePos;
            return;
        }

        _model.localScale    = _modelBaseScale * (1f + _pop);
        _model.localRotation = _modelBaseRot * Quaternion.Euler(SLIDE_PITCH * _slideBlend, 0f, 0f);
        _model.localPosition = _modelBasePos + Vector3.down * (SLIDE_DROP * _slideBlend);
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

            // ── Explicit fallback override ────────────────────────────────────
            if (forceFallback)
            {
                Debug.Log("[RiggedCharacter] forceFallback=true — using InkCultivator.");
                gameObject.AddComponent<InkCultivator>();
                enabled = false;
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
            // Reset to identity scale first.
            instance.transform.localScale = Vector3.one;
            instance.transform.localPosition = Vector3.zero;

            // Force all SkinnedMeshRenderers to update off-screen so bounds are valid.
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in renderers) smr.updateWhenOffscreen = true;

            // CRITICAL: evaluate the animator's current pose before sampling bounds.
            // Without this, bounds reflect the T-pose (or no pose), which is wildly
            // wrong for this model (T-pose tall axis is local-Z, not world-Y).
            _animator.Update(0f);

            if (renderers != null && renderers.Length > 0)
            {
                // Encapsulate all renderer bounds (world space, after pose evaluation).
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                float modelHeight = combined.size.y;
                if (modelHeight > 0.01f)
                {
                    // Uniform scale so the posed height == TARGET_HEIGHT.
                    float scale = TARGET_HEIGHT / modelHeight;
                    instance.transform.localScale = new Vector3(scale, scale, scale);

                    // After scaling, world bounds.min.y shifts.
                    // pre-scale feet offset from player root (world): combined.min.y - transform.position.y
                    // post-scale feet world y = transform.position.y + feetLocalOffset * scale
                    // We want post-scale feet at transform.position.y (i.e. ground / root y).
                    // So: localPosition.y = -feetLocalOffset * scale
                    float feetLocalOffset = combined.min.y - transform.position.y;
                    instance.transform.localPosition = new Vector3(0f, -feetLocalOffset * scale, 0f);

                    Debug.Log($"[RiggedCharacter] Posed bounds height={modelHeight:F4}, scale={scale:F4}, feetLocal={feetLocalOffset:F4}, localY={instance.transform.localPosition.y:F4}");
                }
                else
                {
                    Debug.LogWarning("[RiggedCharacter] SkinnedMesh bounds height is near-zero after pose — skipping scale normalization.");
                }
            }
            else
            {
                Debug.LogWarning("[RiggedCharacter] No SkinnedMeshRenderers found — skipping scale normalization.");
            }

            // Capture the final fitted scale/pose as the base the feel-pop + slide modulate around.
            _modelBaseScale = instance.transform.localScale;
            _modelBasePos   = instance.transform.localPosition;
            _modelBaseRot   = instance.transform.localRotation;

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
        else if (!_runner.Grounded)
        {
            desired = _runner.Vy > 0.5f ? STATE_JUMP : STATE_FALL;  // ascending vs descending
        }
        else
        {
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
