using UnityEngine;

// Builds the entire tracer-bullet scene in code so there's NOTHING to wire in the editor:
// add this one component to one empty GameObject and press Play. Phase 3+ will move these
// into real prefabs/scenes; for the perf test, code assembly is the laziest path to runnable.
public class Bootstrap : MonoBehaviour
{
    void Awake()
    {
        // Managers first (their Awake sets singletons before the player subscribes in Start).
        gameObject.AddComponent<SwipeDetector>();
        gameObject.AddComponent<GameLoop>();
        new GameObject("Ground").AddComponent<Ground>();
        new GameObject("Spawner").AddComponent<Spawner>();

        BuildLight();
        BuildPlayer();
        BuildCamera();
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
    }

    void BuildLight()
    {
        var go = new GameObject("Sun");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }
}
