// OrbVisual.cs — Qi orb idle animation: sin-bob, emission pulse, halo billboard.
// Attached once in Spawner.AcquireOrb(); Init() resets base position on every pool re-use.
// Pool-safe: no state leaks — Init() is the only reset needed.
// ponytail: this is the "orb glow / particle trail VFX" deferred in OrbPickup.cs

using UnityEngine;

public class OrbVisual : MonoBehaviour
{
    // ── Bob / pulse params ──────────────────────────────────────────────────
    const float BOB_AMP   = 0.08f;  // ±8 cm vertical bob
    const float BOB_SPEED = 2.1f;   // radians/s
    const float PULSE_AMP = 0.06f;  // ±6% scale pulse
    const float PULSE_SPEED = 3.4f; // slightly different frequency from bob — feels alive

    // ── Halo params ─────────────────────────────────────────────────────────
    const float HALO_WORLD_SIZE = 0.7f;  // diameter in world units — kept tight so orbs stay distinct
    const float HALO_BASE_ALPHA = 0.38f; // subtle additive glow, not a white blob
    const float HALO_PULSE_AMP  = 0.15f; // halo breathes too

    // ── State ────────────────────────────────────────────────────────────────
    Vector3  _basePos;       // world position at Init time (reset each pool re-use)
    float    _phase;         // per-orb phase offset so trail ripples, not locksteps
    float    _baseScale;     // original local scale from spawn
    SpriteRenderer _halo;    // lazy-created once, re-used via pooling

    // Qi gold: same tint the Spawner uses, set from outside once.
    static readonly Color QI_COLOR = new Color(1.00f, 0.82f, 0.20f, 1f);

    // ── Pool-reset entry point ───────────────────────────────────────────────
    /// <summary>
    /// Call after positioning the orb each time it's acquired from the pool.
    /// Resets the bob anchor and phase so motion doesn't drift across pool re-uses.
    /// </summary>
    public void Init(float phase)
    {
        _phase     = phase;
        _basePos   = transform.position;
        _baseScale = transform.localScale.x; // uniform sphere scale
        EnsureHalo();
    }

    void Update()
    {
        float t = Time.time;

        // ── Bob (Y sin wave) ────────────────────────────────────────────────
        // Yield position while the magnet powerup pulls orbs (Spawner.MagnetPull
        // writes transform.position) — otherwise this snaps the orb back to _basePos
        // every frame and cancels the pull.
        if (Game.I == null || !Game.I.IsPowerupActive("magnet"))
        {
            float bob = Mathf.Sin(t * BOB_SPEED + _phase) * BOB_AMP;
            Vector3 pos = _basePos;
            pos.y += bob;
            transform.position = pos;
        }

        // ── Scale pulse ─────────────────────────────────────────────────────
        float pulse = 1f + Mathf.Sin(t * PULSE_SPEED + _phase) * PULSE_AMP;
        float s = _baseScale * pulse;
        transform.localScale = new Vector3(s, s, s);

        // ── Halo pulse ──────────────────────────────────────────────────────
        if (_halo != null)
        {
            float haloPulse = HALO_BASE_ALPHA + Mathf.Sin(t * PULSE_SPEED + _phase + 0.8f) * HALO_PULSE_AMP;
            Color c = _halo.color;
            c.a = Mathf.Clamp01(haloPulse);
            _halo.color = c;
        }
    }

    // ── Halo setup (once per orb lifetime, pooled forever after) ────────────
    void EnsureHalo()
    {
        if (_halo != null) return; // already built for this pooled object

        var haloGO = new GameObject("OrbHalo");
        haloGO.transform.SetParent(transform, false);
        haloGO.transform.localPosition = Vector3.zero;
        haloGO.transform.localRotation = Quaternion.identity;
        // World-space size: divide by parent's scale so the halo is HALO_WORLD_SIZE in world units
        float invScale = (_baseScale > 0.001f) ? (HALO_WORLD_SIZE / _baseScale) : 2.5f;
        haloGO.transform.localScale = new Vector3(invScale, invScale, invScale);

        var sr = haloGO.AddComponent<SpriteRenderer>();
        sr.sprite = InkArt.SoftGlow(64); // 64px Gaussian glow sprite, cached by InkArt
        sr.color  = new Color(QI_COLOR.r, QI_COLOR.g, QI_COLOR.b, HALO_BASE_ALPHA);

        // Additive blending — glows through fog like a real light source.
        var mat = new Material(Shader.Find("Sprites/Default"));
        // Sprites/Default uses SrcAlpha OneMinusSrcAlpha by default.
        // Switch blend to Additive so the halo adds to whatever's behind it.
        mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite",    0);
        sr.material = mat;

        // Billboard: face the camera. We do this in LateUpdate via a simple
        // LookAt so it works on any camera (game cam, editor cam).
        haloGO.AddComponent<HaloBillboard>();

        _halo = sr;
    }
}

// Tiny billboard helper — keeps the halo facing the camera each frame.
public class HaloBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = cam.transform.rotation;
    }
}
