using UnityEngine;
using UnityEngine.Rendering;

// Ambient atmosphere: sparse drifting spirit-motes + low ground-fog wisps.
// Both are code-built ParticleSystems parented to the camera in LOCAL sim space, so they
// drift gently around the view regardless of run speed (world-space would streak like snow).
// Attach to the Main Camera (Bootstrap does this). Restraint is the brief — keep counts low.
public class Atmosphere : MonoBehaviour
{
    void Start()
    {
        var glow = InkArt.SoftGlow(64).texture;
        BuildMotes(glow);
        BuildFog(glow);
    }

    // Faint jade qi-motes hanging in the air.
    void BuildMotes(Texture2D glow)
    {
        var go = new GameObject("SpiritMotes");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -10f); // out in front of the camera

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 6f;
        main.startSpeed = 0.18f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
        main.startColor = new Color(0.6f, 1f, 0.85f, 0.7f);
        main.maxParticles = 90;
        main.gravityModifier = 0f;

        var em = ps.emission; em.rateOverTime = 16f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(20f, 11f, 18f);
        sh.randomDirectionAmount = 1f; // drift every which way, gently

        // All three axes must share one curve mode or Unity rejects the module
        // ("Particle Velocity curves must all be in the same mode" every frame).
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(0f);
        vol.y = new ParticleSystem.MinMaxCurve(0.08f); // faint upward rise
        vol.z = new ParticleSystem.MinMaxCurve(0f);

        // Twinkle: fade in then out so motes don't pop.
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Apply(ps, ParticleMat(glow, additive: true), 200);
        ps.Play();
    }

    // Low, slow mist rolling near the road surface — adds depth layering at the camera.
    void BuildFog(Texture2D glow)
    {
        var go = new GameObject("GroundFog");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -4f, -9f); // down at road level, ahead

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 9f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
        // 0.30 alpha: the old 0.06 was authored against a broken runtime blend state
        // that rendered far brighter than specified in the editor; with the correct
        // serialized material it was near-invisible on every platform.
        main.startColor = new Color(0.55f, 0.6f, 0.75f, 0.30f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 30;
        main.gravityModifier = 0f;

        var em = ps.emission; em.rateOverTime = 5f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(22f, 1f, 20f);

        // All three axes in TwoConstants mode — mixing modes makes Unity reject the
        // whole module (the per-frame warning spam in device logs) and the drift dies.
        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vol.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var rot = ps.rotationOverLifetime; rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f); // slow churn

        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Apply(ps, ParticleMat(glow, additive: false), 100);
        ps.Play();
        _fog = ps;
    }

    // One-time device diagnostic (Xcode console): proves whether the fog system is
    // alive and what material state it renders with — the wisps have already been
    // invisible-on-device twice for different reasons.
    ParticleSystem _fog;
    bool _loggedFog;

    void Update()
    {
        if (_loggedFog || _fog == null || Time.timeSinceLevelLoad < 5f) return;
        _loggedFog = true;
        var r = _fog.GetComponent<ParticleSystemRenderer>();
        Debug.Log("[Atmosphere] fog: alive=" + _fog.particleCount
            + " playing=" + _fog.isPlaying
            + " shader=" + (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "NULL")
            + " srcBlend=" + (r != null && r.sharedMaterial != null ? r.sharedMaterial.GetInt("_SrcBlend") : -1)
            + " dstBlend=" + (r != null && r.sharedMaterial != null ? r.sharedMaterial.GetInt("_DstBlend") : -1)
            + " tex=" + (r != null && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null ? r.sharedMaterial.mainTexture.name + "/" + r.sharedMaterial.mainTexture.width : "NULL"));
    }

    static void Apply(ParticleSystem ps, Material mat, int sortOffset)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = sortOffset;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // Unlit transparent particle material. Cloned from the serialized keeper material
    // (Resources/ShaderKeep) — its URP transparent setup (keywords + blend state) is
    // baked at import, so device builds render it exactly like the editor. Building
    // the URP particle material entirely at runtime proved flaky on iOS (fog wisps
    // rendered in the editor but not on device).
    static Material ParticleMat(Texture2D tex, bool additive)
    {
        var baseMat = Resources.Load<Material>("ShaderKeep/ParticlesUnlit_Transparent");
        Material m;
        if (baseMat != null)
        {
            m = new Material(baseMat);
        }
        else
        {
            // Fallback: legacy runtime construction (editor-only safety net).
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            m = new Material(sh);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        }
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }
}
