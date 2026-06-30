using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds the entire tracer-bullet scene in code so there's NOTHING to wire in the editor:
// add this one component to one empty GameObject and press Play. Phase 3+ will move these
// into real prefabs/scenes; for the perf test, code assembly is the laziest path to runnable.
public class Bootstrap : MonoBehaviour
{
    // Auto-spawn on Play so the game runs from ANY open scene with zero editor wiring —
    // no need to place a Bootstrap GameObject by hand. Skips if one was placed manually.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBoot()
    {
        if (FindObjectOfType<Bootstrap>() == null)
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
    }

    void Awake()
    {
        // Mobile defaults to 30 FPS — force 60 so on-device input/motion feels responsive.
        // (Editor/Device Simulator ignore this; it matters in the real iOS build.)
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // Managers first (their Awake sets singletons before the player subscribes in Start).
        gameObject.AddComponent<SwipeDetector>();
        gameObject.AddComponent<GameLoop>();
        gameObject.AddComponent<Game>();        // run-state core (#2)
        gameObject.AddComponent<NetOverlay>(); // Heavenly Net closing-in edge overlay (below HUD)
        gameObject.AddComponent<HudOverlay>(); // Ink & Talisman in-run HUD (UI-4)
        gameObject.AddComponent<MainMenu>();   // title screen shown at launch (UI-5)
        gameObject.AddComponent<MenuScreens>(); // shop / journal / settings overlay panels
        gameObject.AddComponent<PauseMenu>();  // in-run pause screen (sortingOrder 23)
        gameObject.AddComponent<CoachMarks>(); // coach-mark tutorial overlay (issue #10, sortingOrder 12)
        gameObject.AddComponent<SoundManager>(); // audio SFX hub (#18)
        gameObject.AddComponent<Music>();        // music shaping (#18)
        new GameObject("Ground").AddComponent<Ground>();
        new GameObject("Scenery").AddComponent<Scenery>();
        new GameObject("GateLandmark").AddComponent<GateLandmark>();
        new GameObject("Spawner").AddComponent<Spawner>();
        new GameObject("TelegraphSystem").AddComponent<TelegraphSystem>();

        BuildLight();
        BuildPlayer();
        BuildCamera();
        BuildAtmosphere();
        BuildPostFX();
    }

    void BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.2f, 0f);

        var cc = player.AddComponent<CharacterController>();
        cc.radius = 0.4f; cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
        cc.slopeLimit = 60f; cc.stepOffset = 0.3f; cc.skinWidth = 0.05f;

        // Visual: a gold capsule (feet at y=0). Strip its collider — the CharacterController owns collision.
        var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        vis.name = "Visual";
        Destroy(vis.GetComponent<Collider>());
        vis.transform.SetParent(player.transform, false);
        vis.transform.localPosition = new Vector3(0f, 1f, 0f);
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        vis.GetComponent<Renderer>().sharedMaterial = new Material(sh) { color = new Color(0.95f, 0.82f, 0.2f) };

        var runner = player.AddComponent<PlayerRunner>();
        runner.visual = vis.transform;

        // Qi aura: a soft jade point light riding the player — pools light on the stone road,
        // separates him from the dark, and reads as cultivation energy. (No shadows; one cheap light.)
        var qiGo = new GameObject("QiGlow");
        qiGo.transform.SetParent(player.transform, false);
        qiGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var qi = qiGo.AddComponent<Light>();
        qi.type = LightType.Point;
        qi.color = new Color(0.45f, 0.95f, 0.75f);
        qi.range = 3.8f;      // tighter pool so it doesn't wash out the hero's own grounding shadow
        qi.intensity = 1.4f;  // dimmer: jade glow still reads, contact shadow survives
        qi.shadows = LightShadows.None;

        player.AddComponent<RiggedCharacter>(); // rigged martial-artist (falls back to InkCultivator if prefab missing)
    }

    void BuildCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.transform.position = new Vector3(0f, 5f, 8f);
        if (cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>();
        if (cam.GetComponent<Atmosphere>() == null) cam.gameObject.AddComponent<Atmosphere>();
    }

    void BuildLight()
    {
        var go = new GameObject("Sun");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.72f, 0.55f);
        l.intensity = 1.1f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.6f;
        go.transform.rotation = Quaternion.Euler(38f, 150f, 0f);  // from upper-right-behind → shadow falls forward toward camera, clearly grounds character

        // Cool rim/back light: shines from ahead-above toward the camera (+Z), so it catches
        // the BACK edges of the player (he faces -Z) and the trees — silhouette separation at dusk.
        var rimGo = new GameObject("RimLight");
        var rim = rimGo.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(0.55f, 0.72f, 1f); // cool moonlight
        rim.intensity = 0.6f;
        rim.shadows = LightShadows.None;        // rim only, no second shadow set
        rimGo.transform.rotation = Quaternion.Euler(20f, 8f, 0f);
    }

    void BuildAtmosphere()
    {
        // Dusk fog — dense purple-grey that dissolves the horizon into the sky colour.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.16f, 0.13f, 0.18f);
        RenderSettings.fogDensity = 0.018f;

        // Flat ambient to keep shadowed geometry readable at dusk.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.22f, 0.30f);

        // Camera background — solid dark dusk so the horizon dissolves seamlessly into fog.
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.09f, 0.14f);
        }
    }

    void BuildPostFX()
    {
        try
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Enable HDR and post-processing on the URP camera data.
            cam.allowHDR = true;
            var urpData = cam.GetUniversalAdditionalCameraData();
            urpData.renderPostProcessing = true;

            // Global Volume — priority 1 so it wins over any scene defaults.
            var go = new GameObject("PostFX");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;

            // Build the profile entirely in code (no asset reference needed).
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = profile;

            // Bloom — makes glowing hazards and the qi-core actually halo.
            // Raised threshold + lowered intensity keeps shape on gate/hazards instead of blowing out.
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            // Subtle warm cinematic grade.
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(12f);
            ca.saturation.Override(-5f);

            // Vignette — light corner darkening, cheap on mobile.
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.5f);

            Debug.Log("[Bootstrap] PostFX volume built successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Bootstrap] PostFX setup failed: " + ex);
        }
    }
}
