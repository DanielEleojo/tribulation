// InkArt.cs — Procedural art toolkit for the "Ink & Talisman" visual style.
// Static helper — no MonoBehaviour. All Texture2D/Sprite generated once and cached.
// Fonts loaded once from Resources and cached in static fields.
//
// note: all texture sizes are small (<=256px); generation is lazy and O(w*h).
// note: SetPixels bulk-writes per texture — no per-pixel SetPixel calls in production paths.

using UnityEngine;
using UnityEngine.UI;

public static class InkArt
{
    // ── Palette ─────────────────────────────────────────────────────────────
    public static readonly Color Parchment     = HexCol("#f2e8d0");
    public static readonly Color ParchmentDark = HexCol("#e3d2ac");
    public static readonly Color Ink           = HexCol("#1a1008");
    public static readonly Color Jade          = HexCol("#2a7c6f");
    public static readonly Color JadeLight     = HexCol("#4db89e");
    public static readonly Color Cinnabar      = HexCol("#c0392b");
    public static readonly Color Gold          = HexCol("#b8860b");
    public static readonly Color TextDim       = HexCol("#6b4e2a");

    // ── Font cache ───────────────────────────────────────────────────────────
    static Font _serif;
    static Font _seal;

    /// <summary>Elegant Latin serif for all UI text. Cache once, reuse everywhere.</summary>
    public static Font Serif()
    {
        if (_serif == null)
            _serif = Resources.Load<Font>("Fonts/InkSerif");
        // Fallback so nothing hard-crashes in editor without the asset.
        if (_serif == null)
            _serif = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _serif;
    }

    /// <summary>Traditional-Chinese subset seal font (23 glyphs only — see task spec).</summary>
    public static Font Seal()
    {
        if (_seal == null)
            _seal = Resources.Load<Font>("Fonts/InkSeal");
        if (_seal == null)
            _seal = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _seal;
    }

    // ── Sprite caches ────────────────────────────────────────────────────────
    // Parchment is cached by (w,h) key; ring/glow/circle by size.
    // RoundedPanel is not cached (varies by radius/border); callers keep ref.
    static System.Collections.Generic.Dictionary<long, Sprite> _parchCache
        = new System.Collections.Generic.Dictionary<long, Sprite>();
    static System.Collections.Generic.Dictionary<int, Sprite> _ringCache
        = new System.Collections.Generic.Dictionary<int, Sprite>();
    static System.Collections.Generic.Dictionary<int, Sprite> _glowCache
        = new System.Collections.Generic.Dictionary<int, Sprite>();
    static System.Collections.Generic.Dictionary<int, Sprite> _circleCache
        = new System.Collections.Generic.Dictionary<int, Sprite>();
    static Sprite _brushStroke128x16; // single canonical brush stroke texture

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Warm parchment fill: base parchment color modulated by multi-octave Perlin value noise
    /// (±~6% lightness) plus a subtle darkening vignette toward edges. Opaque.
    /// </summary>
    public static Sprite ParchmentSprite(int w, int h)
    {
        long key = ((long)w << 32) | (uint)h;
        if (_parchCache.TryGetValue(key, out var cached)) return cached;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        float seed = 13.7f; // offset so parchment looks unique from other Perlin uses

        for (int i = 0; i < pixels.Length; i++)
        {
            int px = i % w, py = i / w;
            float u = (float)px / w;
            float v = (float)py / h;

            // Multi-octave value noise: two octaves, ±6% lightness combined.
            float n1 = Mathf.PerlinNoise(u * 6f + seed,  v * 6f + seed)  * 0.04f;
            float n2 = Mathf.PerlinNoise(u * 13f + seed, v * 13f + seed) * 0.02f;
            float noise = n1 + n2 - 0.03f; // centered ~0, range ±0.06

            // Vignette: darken toward edges (corner distance normalized 0..1).
            float ex = Mathf.Abs(u - 0.5f) * 2f; // 0 at center, 1 at edge
            float ey = Mathf.Abs(v - 0.5f) * 2f;
            float vignette = 0.04f * (ex * ex + ey * ey); // up to ~8% darkening at corners

            Color base_ = InkArt.Parchment; // warm parchment base
            float r = Mathf.Clamp01(base_.r + noise - vignette);
            float g = Mathf.Clamp01(base_.g + noise - vignette);
            float b = Mathf.Clamp01(base_.b + noise - vignette);
            pixels[i] = new Color(r, g, b, 1f);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        _parchCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Parchment-filled rounded rectangle with rounded-corner alpha mask and
    /// an ink-colored border <paramref name="border"/> px thick. NOT cached
    /// (callers vary radius/border freely) — callers should keep the reference.
    /// </summary>
    public static Sprite RoundedPanel(int w, int h, int radius, int border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        // Pre-sample a parchment sprite for the fill colours.
        // We generate inline here (not from cache) to avoid rounding to a different size.
        Color[] pixels = new Color[w * h];
        float seed = 13.7f;

        for (int i = 0; i < pixels.Length; i++)
        {
            int px = i % w, py = i / w;
            float u = (float)px / w;
            float v = (float)py / h;

            // Determine rounded-rect alpha mask: corner distance test.
            // Nearest corner-center offsets.
            int cx = (px < radius) ? radius : (px >= w - radius ? w - 1 - radius : -1);
            int cy = (py < radius) ? radius : (py >= h - radius ? h - 1 - radius : -1);

            bool inCorner = (cx >= 0 && cy >= 0);
            bool insideMask;
            float distFromEdge; // distance inward from mask boundary

            if (inCorner)
            {
                int dx = px - cx, dy = py - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                insideMask    = dist <= radius;
                distFromEdge  = radius - dist;
            }
            else
            {
                insideMask   = true;
                // Distance from nearest straight edge
                float dl = px, dr = w - 1 - px, dt = py, db = h - 1 - py;
                distFromEdge = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(dt, db));
            }

            if (!insideMask)
            {
                pixels[i] = new Color(0, 0, 0, 0);
                continue;
            }

            // Border pixels: draw ink outline.
            bool isBorder = distFromEdge < border;

            if (isBorder)
            {
                // Ink border with slight AA at the very inner edge.
                float aa = Mathf.Clamp01(distFromEdge); // 0..1 within first pixel
                pixels[i] = new Color(Ink.r, Ink.g, Ink.b, aa < 1f ? aa : 1f);
                continue;
            }

            // Parchment fill with noise + vignette.
            float n1 = Mathf.PerlinNoise(u * 6f + seed,  v * 6f + seed)  * 0.04f;
            float n2 = Mathf.PerlinNoise(u * 13f + seed, v * 13f + seed) * 0.02f;
            float noise = n1 + n2 - 0.03f;
            float ex = Mathf.Abs(u - 0.5f) * 2f;
            float ey = Mathf.Abs(v - 0.5f) * 2f;
            float vignette = 0.04f * (ex * ex + ey * ey);

            Color base_ = InkArt.Parchment;
            pixels[i] = new Color(
                Mathf.Clamp01(base_.r + noise - vignette),
                Mathf.Clamp01(base_.g + noise - vignette),
                Mathf.Clamp01(base_.b + noise - vignette),
                1f);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Horizontal qi brush-stroke: white (tinted by vertex/line color), full alpha along
    /// centerline, Gaussian falloff vertically, tapered/irregular alpha at far left and right
    /// ends for calligraphic feel. Cache a canonical 128×16 version.
    /// </summary>
    public static Sprite BrushStroke(int w = 128, int h = 16)
    {
        // note: only the canonical 128×16 is cached; other sizes recreated.
        if (w == 128 && h == 16 && _brushStroke128x16 != null)
            return _brushStroke128x16;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        float cy = h * 0.5f;
        float sigma = h * 0.28f; // Gaussian sigma — controls feather width

        for (int i = 0; i < pixels.Length; i++)
        {
            int px = i % w, py = i / w;

            // Vertical Gaussian falloff from centerline.
            float dy = py - cy;
            float gaussV = Mathf.Exp(-(dy * dy) / (2f * sigma * sigma));

            // Horizontal taper: fade out the left ~12% and right ~12% of the stroke.
            float u = (float)px / (w - 1);
            float taperL = Mathf.SmoothStep(0f, 1f, u / 0.12f);
            float taperR = Mathf.SmoothStep(0f, 1f, (1f - u) / 0.12f);

            // Slight irregularity: a tiny Perlin wiggle on the horizontal taper seam
            // so it reads as hand-drawn rather than mechanically smooth.
            float irregularity = Mathf.PerlinNoise(u * 18f, 0.5f) * 0.08f;

            float alpha = gaussV * taperL * taperR * (1f - irregularity);
            alpha = Mathf.Clamp01(alpha);

            pixels[i] = new Color(1f, 1f, 1f, alpha); // white — tinted by Image/Line color
        }

        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        if (w == 128 && h == 16)
            _brushStroke128x16 = sprite;
        return sprite;
    }

    /// <summary>
    /// Richer talisman seal ring: two concentric circle outlines (outer thick ~3px, inner ~1px),
    /// ~16 short radial tick-marks between them, and ~4 longer radial spokes. White (tinted by
    /// Image.color). Transparent elsewhere. Cached by size.
    /// </summary>
    public static Sprite SealRing(int size)
    {
        if (_ringCache.TryGetValue(size, out var cached)) return cached;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        // Start transparent.
        Color clear = new Color(0, 0, 0, 0);
        Color draw  = Color.white;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        int cx = size / 2, cy = size / 2;
        float outer = size * 0.47f;  // outer ring radius
        float inner = size * 0.37f;  // inner ring radius (ticks live between outer and inner)
        int   outerThick = Mathf.Max(2, size / 30); // outer ring thickness ~3px at 88px
        int   innerThick = 1;

        // Draw outer ring (thick).
        DrawCircleOutline(tex, cx, cy, (int)outer, outerThick, draw);
        // Draw inner ring (thin).
        DrawCircleOutline(tex, cx, cy, (int)inner, innerThick, draw);

        // 16 short radial tick-marks between inner and outer rings.
        float tickOuter = outer - outerThick;
        float tickInner = inner + innerThick + size * 0.02f; // slight gap from inner ring
        for (int t = 0; t < 16; t++)
        {
            float angle = t * Mathf.PI * 2f / 16f;
            float cosA  = Mathf.Cos(angle);
            float sinA  = Mathf.Sin(angle);
            DrawRadialSegment(tex, cx, cy, cosA, sinA, tickInner, tickOuter, draw);
        }

        // 4 longer radial spokes crossing through to near center.
        float spokeOuter = outer - outerThick;
        float spokeInner = size * 0.06f;
        for (int s = 0; s < 4; s++)
        {
            float angle = s * Mathf.PI * 2f / 4f + Mathf.PI / 8f; // offset 22.5° for elegance
            float cosA  = Mathf.Cos(angle);
            float sinA  = Mathf.Sin(angle);
            DrawRadialSegment(tex, cx, cy, cosA, sinA, spokeInner, spokeOuter, draw);
        }

        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _ringCache[size] = sprite;
        return sprite;
    }

    /// <summary>
    /// Radial white→transparent Gaussian glow for halos behind seal-ring / qi-ready.
    /// Cached by size.
    /// </summary>
    public static Sprite SoftGlow(int size)
    {
        if (_glowCache.TryGetValue(size, out var cached)) return cached;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;
        float sigma = size * 0.25f; // controls glow radius

        for (int i = 0; i < pixels.Length; i++)
        {
            int px = i % size, py = i / size;
            float dx = px - cx, dy = py - cy;
            float distSq = dx * dx + dy * dy;
            float alpha = Mathf.Exp(-distSq / (2f * sigma * sigma));
            pixels[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _glowCache[size] = sprite;
        return sprite;
    }

    /// <summary>
    /// Solid white circle for the radial-fill disk. Cached by size.
    /// </summary>
    public static Sprite SolidCircle(int size)
    {
        if (_circleCache.TryGetValue(size, out var cached)) return cached;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        int cx2 = size / 2, cy2 = size / 2;
        float rSq = (size * 0.5f - 1f) * (size * 0.5f - 1f);

        for (int i = 0; i < pixels.Length; i++)
        {
            int px = i % size, py = i / size;
            int dx = px - cx2, dy = py - cy2;
            pixels[i] = (dx * dx + dy * dy <= rSq) ? Color.white : new Color(0, 0, 0, 0);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _circleCache[size] = sprite;
        return sprite;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Draws a circle outline of given thickness onto tex (SetPixel — called once at build).
    static void DrawCircleOutline(Texture2D tex, int cx, int cy, int radius, int thick, Color col)
    {
        float rOuter = radius + thick * 0.5f;
        float rInner = radius - thick * 0.5f;
        float rOSq   = rOuter * rOuter;
        float rISq   = rInner * rInner;

        int size = tex.width;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = x - cx, dy = y - cy;
                float dSq = dx * dx + dy * dy;
                if (dSq <= rOSq && dSq >= rISq)
                    tex.SetPixel(x, y, col);
            }
        }
    }

    // Draws a radial line segment from radius r0 to r1 on a given angle (cosA/sinA).
    static void DrawRadialSegment(Texture2D tex, int cx, int cy,
        float cosA, float sinA, float r0, float r1, Color col)
    {
        int steps = Mathf.RoundToInt(r1 - r0) + 2;
        for (int s = 0; s <= steps; s++)
        {
            float r = Mathf.Lerp(r0, r1, (float)s / steps);
            int px = Mathf.RoundToInt(cx + cosA * r);
            int py = Mathf.RoundToInt(cy + sinA * r);
            int size = tex.width;
            if (px >= 0 && px < size && py >= 0 && py < size)
                tex.SetPixel(px, py, col);
        }
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    /// <summary>Adds a Shadow component (ink color) to a UI element for legibility.</summary>
    public static Shadow AddShadow(UnityEngine.UI.Graphic target, float distance = 1.2f)
    {
        var shadow = target.gameObject.AddComponent<Shadow>();
        shadow.effectColor    = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
        shadow.effectDistance = new Vector2(distance, -distance);
        return shadow;
    }

    /// <summary>Adds an Outline component (ink color) for maximum legibility on mixed backgrounds.</summary>
    public static Outline AddOutline(UnityEngine.UI.Graphic target, float distance = 0.8f)
    {
        var outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor    = new Color(Ink.r, Ink.g, Ink.b, 0.70f);
        outline.effectDistance = new Vector2(distance, -distance);
        return outline;
    }

    // Parses "#RRGGBB" hex string into Color.
    static Color HexCol(string hex)
    {
        hex = hex.TrimStart('#');
        float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }
}
