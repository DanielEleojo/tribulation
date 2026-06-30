// Life/Death Gate — three translucent lane curtains + minimal torii frame.
// Ported from gate.gd (78 lines). One safe (green) panel, two lethal (red) panels.
// Resolves exactly once per gate object (_resolved guard). Calls Game.I.OnGate(safe)
// on first player contact, then destroys itself.
//
// ponytail: exact torii proportions, talisman glow, HUD flash, sfx, camera shake — deferred
// ponytail: translucency requires URP Transparent surface mode; using a simple opaque
//   emissive-tinted material here since MakeMat() in Spawner uses the same approach.
//   A proper URP transparent pass would need shader keyword "_SURFACE_TYPE_TRANSPARENT"
//   and render queue 3000 — deferred to visual polish issue.
// note: gate pooling — low-frequency spawn (11s interval), plain Instantiate/Destroy is fine.

using UnityEngine;

public class Gate : MonoBehaviour
{
    // ── Constants (mirrors gate.gd) ──────────────────────────────────────────
    const float LANE_WIDTH = 2.5f;
    const float PANEL_W    = 2.4f;
    const float PANEL_H    = 4.0f;
    const float PANEL_D    = 0.4f;

    static readonly Color SAFE_COLOR  = new Color(0.2f, 0.9f, 0.4f,  0.45f);
    static readonly Color DEATH_COLOR = new Color(0.9f, 0.15f, 0.2f, 0.50f);

    // Torii frame: dark wood posts + lintel (gate.gd _gate_box calls)
    static readonly Color POST_COLOR   = new Color(0.24f, 0.10f, 0.07f);
    static readonly Color LINTEL_COLOR = new Color(0.45f, 0.12f, 0.09f);

    // ── State ────────────────────────────────────────────────────────────────
    bool _resolved;

    // ── Setup (called by Spawner after Instantiate) ───────────────────────────
    /// <summary>
    /// Build the three curtains. safe_lane (0/1/2) picks the Life Gate panel.
    /// Mirrors gate.gd setup().
    /// </summary>
    public void Setup(int safeLane)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            bool isSafe = (lane == safeLane);
            float x     = -(lane - 1) * LANE_WIDTH; // mirrors PlayerRunner lane→X formula
            BuildPanel(x, isSafe);
        }
        BuildToriiFrame();
    }

    // ── Panel construction ───────────────────────────────────────────────────

    void BuildPanel(float x, bool safe)
    {
        // Parent node for this panel's trigger + visuals
        var panel = new GameObject(safe ? "Panel_Safe" : "Panel_Death");
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = new Vector3(x, 0f, 0f);

        // Visual box
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "PanelMesh";
        vis.transform.SetParent(panel.transform, false);
        vis.transform.localScale    = new Vector3(PANEL_W, PANEL_H, PANEL_D);
        vis.transform.localPosition = new Vector3(0f, PANEL_H * 0.5f, 0f);
        Object.Destroy(vis.GetComponent<Collider>()); // trigger is on a separate child
        vis.GetComponent<MeshRenderer>().sharedMaterial = MakePanelMat(safe);

        // Trigger (non-blocking) — separate so the mesh collider doesn't interfere
        var triggerGo = new GameObject("Trigger");
        triggerGo.transform.SetParent(panel.transform, false);
        triggerGo.transform.localPosition = new Vector3(0f, PANEL_H * 0.5f, 0f);
        var col = triggerGo.AddComponent<BoxCollider>();
        col.size      = new Vector3(PANEL_W, PANEL_H, PANEL_D);
        col.isTrigger = true;

        // Tag the trigger with safe/death so OnTriggerEnter can read it
        var tag = triggerGo.AddComponent<GatePanelTag>();
        tag.IsSafe = safe;
        tag.Gate   = this;
    }

    // ── Minimal torii frame (two posts + one lintel) ─────────────────────────
    // Ported from gate.gd _gate_box calls on lines 49-51.

    void BuildToriiFrame()
    {
        // Left post
        GateBox(new Vector3(0.25f, PANEL_H, 0.25f),
                new Vector3(-(PANEL_W * 1.5f), PANEL_H * 0.5f, 0f),
                POST_COLOR);
        // Right post
        GateBox(new Vector3(0.25f, PANEL_H, 0.25f),
                new Vector3( (PANEL_W * 1.5f), PANEL_H * 0.5f, 0f),
                POST_COLOR);
        // Lintel
        GateBox(new Vector3(PANEL_W * 3f + 0.7f, 0.35f, 0.4f),
                new Vector3(0f, PANEL_H + 0.1f, 0f),
                LINTEL_COLOR);
    }

    void GateBox(Vector3 size, Vector3 localPos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ToriiPart";
        go.transform.SetParent(transform, false);
        go.transform.localScale    = size;
        go.transform.localPosition = localPos;
        Object.Destroy(go.GetComponent<Collider>()); // purely visual, no collision
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(color);
    }

    // ── Resolution ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GatePanelTag.OnTriggerEnter when the player enters a panel.
    /// Guards against multiple invocations (once-per-gate-pass).
    /// </summary>
    public void Resolve(bool safe)
    {
        if (_resolved) return;
        _resolved = true;
        if (Game.I != null)
            Game.I.OnGate(safe);
        Destroy(gameObject); // low-frequency; plain Destroy is fine (ponytail: no pool)
    }

    // ── Material helpers ─────────────────────────────────────────────────────

    Material MakePanelMat(bool safe)
    {
        Color c = safe ? SAFE_COLOR : DEATH_COLOR;
        // ponytail: URP transparent surface mode deferred; using opaque emissive tint
        var mat = MakeMat(c);
        return mat;
    }

    static Material MakeMat(Color c)
    {
        var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh) { color = c };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 0.5f);
        return mat;
    }
}

/// <summary>
/// Thin component sitting on each panel trigger — routes OnTriggerEnter to Gate.Resolve().
/// Keeps Gate.cs from needing to iterate children.
/// </summary>
public class GatePanelTag : MonoBehaviour
{
    public bool IsSafe;
    public Gate Gate;

    void OnTriggerEnter(Collider other)
    {
        // Only react to the player (PlayerRunner is on the player GameObject)
        if (other.GetComponent<PlayerRunner>() == null) return;
        Gate?.Resolve(IsSafe);
    }
}
