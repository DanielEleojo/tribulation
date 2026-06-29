using UnityEngine;
using UnityEngine.Rendering;
using PrimeTween;

// Implemented by whichever character-visual driver is active (RiggedCharacter or the
// procedural InkCultivator fallback) so PlayerRunner can punch its scale without caring which.
public interface IFeelPose { void Pop(float strength); }

// Game-feel helpers (Feel Pass v1). Built on the already-installed PrimeTween.
public static class Feel
{
    // Brief impact freeze for weight. Restores on UNSCALED time so the restore fires
    // even though the freeze slows scaled time to a crawl.
    // ponytail: global timeScale dip — fine for a single-player runner; revisit only if
    // something gameplay-critical must keep ticking at full rate during the freeze.
    public static void Hitstop(float seconds = 0.06f, float scale = 0.06f)
    {
        if (Time.timeScale == 0f) return; // don't fight the pause menu
        Time.timeScale = scale;
        Tween.Delay(seconds, () => { if (Time.timeScale != 0f) Time.timeScale = 1f; }, useUnscaledTime: true);
    }

    // Cheap dust puff: a flattened sphere that grows then collapses (no transparency needed).
    // ponytail: placeholder until the particle tier swaps in a real ParticleSystem.
    public static void Poof(Vector3 pos, float size = 1.2f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = pos + Vector3.up * 0.1f;
        go.transform.localScale = Vector3.one * 0.15f;
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        if (sh != null) mr.sharedMaterial = new Material(sh) { color = new Color(0.72f, 0.70f, 0.64f) };
        Tween.Scale(go.transform, new Vector3(size, size * 0.4f, size), 0.12f, Ease.OutQuad, 2, CycleMode.Yoyo)
             .OnComplete(() => { if (go != null) Object.Destroy(go); });
    }

    // ── Particle helpers (Feel Pass v2) ─────────────────────────────────────

    // One-shot additive ParticleSystem burst at a world position.
    // ponytail: per-call PS alloc; pool if profiler flags it.
    public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life,
                             float gravity = 0.2f)
    {
        var go = new GameObject("FeelBurst");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();

        // main module
        var main = ps.main;
        main.startLifetime    = life;
        main.startSpeed       = speed;
        main.startSize        = size;
        main.startColor       = color;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.gravityModifier  = gravity;
        main.loop             = false;
        main.playOnAwake      = false;

        // emission: one burst, no continuous rate
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        // shape: small sphere so sparks scatter in all directions
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.1f;

        // colorOverLifetime: fade alpha to zero so particles vanish cleanly
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // additive material — same recipe as Atmosphere.cs / GateLandmark.cs
        var glowTex = InkArt.SoftGlow(32).texture;
        var shader  = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                   ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", glowTex);
            mat.mainTexture = glowTex;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite",  0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One); // additive
            mat.renderQueue = (int)RenderQueue.Transparent;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material          = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows    = false;
        }

        ps.Play();
        ps.Emit(count);
        Object.Destroy(go, life + 0.5f);
    }

    // Hot spark flash for enemy kills: white-gold, sharp and fast.
    public static void Spark(Vector3 pos)
        => Burst(pos, new Color(1f, 0.95f, 0.7f, 1f), count: 14, speed: 6f, size: 0.3f, life: 0.35f, gravity: 0.1f);

    // Jade qi pickup pop: soft teal scatter + a quick expanding ring.
    public static void CollectPop(Vector3 pos)
    {
        Burst(pos, new Color(0.5f, 1f, 0.8f, 1f), count: 10, speed: 3f, size: 0.35f, life: 0.4f, gravity: 0.05f);

        // Expanding ring: additive SoftGlow quad that scales up and fades out.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.transform.position   = pos;
        ring.transform.localScale = Vector3.one * 0.2f;
        ring.transform.rotation   = Quaternion.Euler(90f, 0f, 0f); // lie flat on the ground plane

        var rend = ring.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows    = false;

        var glowTex = InkArt.SoftGlow(64).texture;
        var shader  = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                   ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", glowTex);
            mat.mainTexture = glowTex;
            mat.color = new Color(0.5f, 1f, 0.8f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.5f, 1f, 0.8f, 1f));
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite",  0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One); // additive
            mat.renderQueue = (int)RenderQueue.Transparent;
            rend.sharedMaterial = mat;
        }

        Tween.Scale(ring.transform, Vector3.one * 2f, 0.3f, Ease.OutQuad)
             .OnComplete(() => { if (ring != null) Object.Destroy(ring); });
    }

    // Soft dust kick-up for jump and slide: warm grey-brown, low-gravity drift.
    public static void DustBurst(Vector3 pos)
        => Burst(pos, new Color(0.72f, 0.70f, 0.64f, 0.8f), count: 8, speed: 2f, size: 0.5f, life: 0.5f, gravity: 0.35f);

    // Brief bright crescent flash in front of the player for the slash attack.
    public static void SlashArc(Vector3 pos)
    {
        // Spawn a wide thin streak quad ~0.5 m ahead and ~1 m above the player.
        var arc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(arc.GetComponent<Collider>());
        arc.transform.position   = pos + new Vector3(0f, 1f, -0.5f); // ahead in -Z, chest height
        arc.transform.localScale = new Vector3(2.5f, 0.6f, 1f);
        arc.transform.rotation   = Quaternion.identity;

        var rend = arc.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows    = false;

        var glowTex = InkArt.SoftGlow(64).texture;
        var shader  = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                   ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", glowTex);
            mat.mainTexture = glowTex;
            mat.color = new Color(0.85f, 1f, 1f, 1f); // white-cyan
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.85f, 1f, 1f, 1f));
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite",  0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One); // additive
            mat.renderQueue = (int)RenderQueue.Transparent;
            rend.sharedMaterial = mat;
        }

        // Quick fade + slight scale-up, then destroy — mirrors Feel.Poof's tween+OnComplete pattern.
        Tween.Scale(arc.transform, new Vector3(3.5f, 0.3f, 1f), 0.18f, Ease.OutQuad)
             .OnComplete(() => { if (arc != null) Object.Destroy(arc); });
    }
}
