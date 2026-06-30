// HeavenlyNet.shader — Daoist suppression formation overlay.
// Port of net_overlay.gd (Godot canvas_item shader) to ShaderLab/URP.
// Concentric rings + radial spokes + bagua core + dashed glyph ring,
// spirit-cyan turning urgent gold as _Net approaches 1.
// The formation manifests only OUTSIDE the clear center (rc shrinks as net rises).
//
// ponytail: fullscreen fragment shader — known mobile fill-rate cost; optimize
//   (reduce sample count / lower-res RT) when tackling iOS perf issue #1.

Shader "Tribulation/HeavenlyNet"
{
    Properties
    {
        // UI Image binds a texture to _MainTex every canvas render — declare it (unused) so
        // Unity doesn't log "doesn't have a texture property '_MainTex'" each frame.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Net    ("Net (0..1)",      Range(0, 1)) = 0.0
        _Aspect ("Aspect (w/h)",   Float)        = 0.5625  // 9:16 portrait default
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Properties ─────────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float _Net;
                float _Aspect;  // Screen.width / Screen.height
            CBUFFER_END

            // ── Vertex ─────────────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            // ── Constants (mirrored from Godot source) ─────────────────────────
            #define RINGS   9.0
            #define SPOKES  16.0
            #define R_OCT   0.15    // bagua octagon radius
            #define R_GLYPH 0.55    // talisman/glyph ring radius
            #define TAU     6.28318530718
            #define PI      3.14159265359

            // ── Fragment ─────────────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Convert UV to pixel-space offsets from screen center.
                // Godot uses: p = (UV - 0.5) * screen_size
                // Here we reproduce that with a virtual screen of height=1, width=_Aspect.
                // r is then normalised so that ~1.0 = top/bottom edge (half-height = 0.5 units).
                float2 p = (uv - 0.5) * float2(_Aspect, 1.0);  // pixels-like, height = 1 unit
                float  r   = length(p) / 0.5;                   // 1.0 at top/bottom edge
                float  ang = atan2(p.y, p.x);

                float rc = (1.0 - _Net) * 1.18;  // clear center radius shrinks as net closes

                // ── Concentric ring lines ─────────────────────────────────────
                float dr   = min(frac(r * RINGS), 1.0 - frac(r * RINGS));
                float ring = 1.0 - smoothstep(0.0, 0.05, dr);

                // ── Radial spokes, fade near very center ──────────────────────
                float xa    = (ang / TAU + 0.5) * SPOKES;
                float da    = min(frac(xa), 1.0 - frac(xa));
                float spoke = (1.0 - smoothstep(0.0, 0.05, da)) * smoothstep(0.04, 0.22, r);

                float pattern = max(ring, spoke);

                // ── Bagua core (octagon + 8 trigram ticks + taiji ring) ───────
                float fold  = abs(fmod(ang, PI * 0.25) - PI * 0.125);
                float oct_r = R_OCT * (cos(PI * 0.125) / cos(fold));
                float oct   = 1.0 - smoothstep(0.0, 0.012, abs(r - oct_r));

                float a8  = (ang / TAU + 0.5) * 8.0;
                float d8  = min(frac(a8), 1.0 - frac(a8));
                float bag = (1.0 - smoothstep(0.0, 0.07, d8))
                            * smoothstep(R_OCT, R_OCT * 1.1, r)
                            * (1.0 - smoothstep(R_OCT * 2.0, R_OCT * 2.3, r));

                float taiji = 1.0 - smoothstep(0.0, 0.012, abs(r - 0.055));
                float core  = max(oct, max(bag, taiji));

                // ── Glyph/talisman ring (dashed) ─────────────────────────────
                float gring = 1.0 - smoothstep(0.0, 0.02, abs(r - R_GLYPH));
                float dash  = step(0.55, frac((ang / TAU + 0.5) * 44.0));
                float glyph = gring * dash;

                pattern = max(pattern, max(core, glyph));

                // ── Closing band mask + bright leading edge ring ──────────────
                // Formation only manifests outside the clear center.
                float m    = smoothstep(rc - 0.02, rc + 0.05, r);
                float edge = 1.0 - smoothstep(0.0, 0.035, abs(r - rc));

                float alpha = pattern * m * 0.85 + edge + m * 0.05;

                // ── Colour: spirit-cyan base, glow highlight, gold warn ────────
                float3 col_main = float3(0.50, 0.82, 1.00);  // spirit-cyan
                float3 col_glow = float3(0.88, 0.96, 1.00);  // cold white highlight

                float3 col = lerp(col_main, col_glow, clamp(pattern * 0.6 + edge, 0.0, 1.0));

                // Cyan turns to urgent gold as net approaches 1
                float warn = smoothstep(0.7, 1.0, _Net);
                col = lerp(col, float3(1.0, 0.72, 0.28), warn * 0.55);

                // Final alpha × 0.9 (matches Godot source line 61)
                return float4(col, clamp(alpha, 0.0, 1.0) * 0.9);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
