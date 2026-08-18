// WarriorCharacter.cs — drives the animated wuxia-warrior GLB (Resources/Models/warrior,
// the original Godot-era player model) as the player visual. Middle link of the visual
// fallback chain: RiggedCharacter (Asset Store Solider_Fist, not in git) → THIS →
// InkCultivator (procedural primitives). glTFast imports the .glb with a LEGACY
// Animation component, so clips are driven by name via Animation.CrossFade — no
// Animator controller involved. Pose logic mirrors RiggedCharacter 1:1 (same
// PlayerRunner polling, slash event, death-freeze, feel-pop), except sliding uses the
// warrior's real Slide clip instead of RiggedCharacter's procedural recline.
//
// CONTRACT:
//   • Asset path : Resources/Models/warrior (legacy Animation; clips:
//                  Running, Slash, Death, Jump, Slide, Idle)
//   • CharacterController: height 2, center y=1, feet at y=0 on Player root.

using UnityEngine;

[RequireComponent(typeof(PlayerRunner))]
public class WarriorCharacter : MonoBehaviour, IFeelPose
{
    // ── Legacy clip name constants (as imported from warrior.glb) ─────────────
    const string CLIP_RUN    = "Running";
    const string CLIP_ATTACK = "Slash";
    const string CLIP_DIE    = "Death";
    const string CLIP_SLIDE  = "Slide";
    const string CLIP_JUMP   = "Jump";
    const string CLIP_FALL   = "Jump"; // no dedicated fall clip — held jump reads fine

    // Target character height in world units; matches CharacterController height.
    const float TARGET_HEIGHT = 1.9f;

    // How long the attack animation plays before reverting to run.
    const float SLASH_ANIM_DURATION = 0.5f;

    // How long to hold the death pose before freezing the clip.
    const float DEATH_FREEZE_DELAY = 1.2f;

    // ── Cached references ─────────────────────────────────────────────────────
    PlayerRunner _runner;
    Animation    _anim;   // legacy Animation on the instantiated glb

    // ── State tracking (mirrors RiggedCharacter) ──────────────────────────────
    string _cur = "";
    float  _slashTimer;
    bool   _deathFreezeStarted;
    float  _deathFreezeTimer;
    bool   _frozen;

    // ── Feel Pass: decaying scale-pop on the model root ───────────────────────
    Transform _model;
    Vector3   _modelBaseScale = Vector3.one;
    float     _pop;
    public void Pop(float strength) { _pop = Mathf.Max(_pop, strength); }

    void LateUpdate()
    {
        if (_model == null) return;
        if (_pop > 0.0001f) _pop = Mathf.Lerp(_pop, 0f, Mathf.Clamp01(11f * Time.deltaTime));
        if (_pop < 0.001f)  _pop = 0f;
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

            _runner = GetComponent<PlayerRunner>();
            if (_runner == null)
            {
                Debug.LogError("[WarriorCharacter] No PlayerRunner on this GameObject. Aborting setup.");
                return;
            }

            // ── Load the imported glb ─────────────────────────────────────────
            var prefab = Resources.Load<GameObject>("Models/warrior");
            if (prefab == null || !HasUsableSkinnedMesh(prefab))
            {
                Debug.LogWarning("[WarriorCharacter] Resources/Models/warrior missing or has no usable meshes — falling back to InkCultivator.");
                gameObject.AddComponent<InkCultivator>();
                enabled = false;
                return;
            }

            // ── Instantiate as child; face -Z (run direction, toward camera) ──
            var instance = Instantiate(prefab, transform);
            instance.name = "WarriorModel";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _model = instance.transform;

            // ── Strip colliders from the model (cosmetic only) ────────────────
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            // ── Legacy Animation component ────────────────────────────────────
            _anim = instance.GetComponentInChildren<Animation>();
            if (_anim == null)
            {
                Debug.LogError("[WarriorCharacter] No legacy Animation on warrior glb — falling back to InkCultivator.");
                Destroy(instance);
                gameObject.AddComponent<InkCultivator>();
                enabled = false;
                return;
            }

            // Wrap modes: looping locomotion, clamped one-shots.
            SetWrap(CLIP_RUN,   WrapMode.Loop);
            SetWrap(CLIP_SLIDE, WrapMode.Loop);         // held for the whole slide
            SetWrap("Idle",     WrapMode.Loop);
            SetWrap(CLIP_JUMP,  WrapMode.ClampForever); // hold apex frame on long airtime
            SetWrap(CLIP_ATTACK, WrapMode.Once);
            SetWrap(CLIP_DIE,   WrapMode.ClampForever);

            // ── Normalize scale + grounding (same math as RiggedCharacter) ────
            instance.transform.localScale = Vector3.one;
            instance.transform.localPosition = Vector3.zero;

            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in renderers) smr.updateWhenOffscreen = true;

            // Sample the run pose before measuring bounds — bind-pose bounds are wrong.
            _anim.Play(CLIP_RUN);
            _anim.Sample();

            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                float modelHeight = combined.size.y;
                if (modelHeight > 0.01f)
                {
                    float scale = TARGET_HEIGHT / modelHeight;
                    instance.transform.localScale = new Vector3(scale, scale, scale);
                    float feetLocalOffset = combined.min.y - transform.position.y;
                    instance.transform.localPosition = new Vector3(0f, -feetLocalOffset * scale, 0f);
                }
                else
                {
                    Debug.LogWarning("[WarriorCharacter] SkinnedMesh bounds height near-zero after pose — skipping scale normalization.");
                }
            }

            _modelBaseScale = instance.transform.localScale;

            // ── Start running + subscribe to slash ────────────────────────────
            CrossTo(CLIP_RUN, 0f);
            _runner.Slashed += OnSlashed;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[WarriorCharacter] Setup failed — falling back to InkCultivator. " + ex);
            try { gameObject.AddComponent<InkCultivator>(); }
            catch (System.Exception ex2) { Debug.LogError("[WarriorCharacter] InkCultivator fallback also failed. " + ex2); }
            enabled = false;
        }
    }

    void OnDestroy()
    {
        if (_runner != null)
            _runner.Slashed -= OnSlashed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update — state selection and death-freeze (mirrors RiggedCharacter)
    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (_anim == null || _runner == null) return;

        if (_slashTimer > 0f)
            _slashTimer -= Time.deltaTime;

        // ── Revival: was frozen/dead but runner restarted ─────────────────────
        if (_frozen && !_runner.IsDead)
        {
            _frozen = false;
            _deathFreezeStarted = false;
            _deathFreezeTimer = 0f;
            SetClipSpeed(CLIP_DIE, 1f);
            CrossTo(CLIP_RUN, 0.12f);
            return;
        }

        // ── Death-freeze countdown ────────────────────────────────────────────
        if (_deathFreezeStarted && !_frozen)
        {
            _deathFreezeTimer -= Time.deltaTime;
            if (_deathFreezeTimer <= 0f)
            {
                _frozen = true;
                SetClipSpeed(CLIP_DIE, 0f); // hold the current death frame
            }
        }

        if (_frozen) return;

        // ── State selection ───────────────────────────────────────────────────
        string desired;
        if (_runner.IsDead)            desired = CLIP_DIE;
        else if (_slashTimer > 0f)     desired = CLIP_ATTACK;
        else if (_runner.IsSliding)    desired = CLIP_SLIDE;
        else if (!_runner.Grounded)    desired = _runner.Vy > 0.5f ? CLIP_JUMP : CLIP_FALL;
        else                           desired = CLIP_RUN;

        if (desired != _cur)
        {
            CrossTo(desired, 0.12f);
            if (desired == CLIP_DIE && !_deathFreezeStarted)
            {
                _deathFreezeStarted = true;
                _deathFreezeTimer = DEATH_FREEZE_DELAY;
            }
        }
    }

    void OnSlashed()
    {
        _slashTimer = SLASH_ANIM_DURATION;
        if (_anim != null)
        {
            // Rewind so back-to-back slashes restart the swing instead of no-oping.
            _anim.Rewind(CLIP_ATTACK);
            _anim.CrossFade(CLIP_ATTACK, 0.06f);
        }
        _cur = CLIP_ATTACK;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    void CrossTo(string clip, float blend)
    {
        if (_anim == null) return;
        if (blend <= 0f) _anim.Play(clip);
        else             _anim.CrossFade(clip, blend);
        _cur = clip;
    }

    void SetWrap(string clip, WrapMode mode)
    {
        var st = _anim[clip];
        if (st != null) st.wrapMode = mode;
    }

    void SetClipSpeed(string clip, float speed)
    {
        var st = _anim != null ? _anim[clip] : null;
        if (st != null) st.speed = speed;
    }

    static bool HasUsableSkinnedMesh(GameObject prefab)
    {
        foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.sharedMesh != null) return true;
        return false;
    }
}
