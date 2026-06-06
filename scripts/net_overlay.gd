extends CanvasLayer
## The Heavenly Net: a daoist suppression formation (concentric rings + radial spokes,
## cyan spirit-light) that contracts from the screen edges toward the center as the net
## tightens. net = 0 -> clear; net = 1 -> the formation closes over the center (death).
## Inspired by assets/props/net_formation.png. Driven by game.gd via on_net_changed().

const SHADER_CODE := "
shader_type canvas_item;
render_mode blend_mix;
uniform float net : hint_range(0.0, 1.0) = 0.0;
uniform vec2 screen_size = vec2(720.0, 1280.0);
uniform vec4 col_main : source_color = vec4(0.50, 0.82, 1.0, 1.0);
uniform vec4 col_glow : source_color = vec4(0.88, 0.96, 1.0, 1.0);
const float RINGS = 9.0;
const float SPOKES = 16.0;
const float R_OCT = 0.15;     // bagua octagon radius
const float R_GLYPH = 0.55;   // talisman/glyph ring radius
void fragment() {
	vec2 p = (UV - 0.5) * screen_size;             // pixels from center
	float r = length(p) / (0.5 * screen_size.y);   // ~1.0 at top/bottom edge
	float ang = atan(p.y, p.x);
	float rc = (1.0 - net) * 1.18;                 // clear center radius shrinks as it closes

	// Concentric ring lines.
	float dr = min(fract(r * RINGS), 1.0 - fract(r * RINGS));
	float ring = 1.0 - smoothstep(0.0, 0.05, dr);
	// Radial spokes, fading out near the very center.
	float xa = (ang / TAU + 0.5) * SPOKES;
	float da = min(fract(xa), 1.0 - fract(xa));
	float spoke = (1.0 - smoothstep(0.0, 0.05, da)) * smoothstep(0.04, 0.22, r);
	float pattern = max(ring, spoke);

	// --- Bagua core (octagon + 8 trigram ticks + taiji ring) ---
	float fold = abs(mod(ang, PI * 0.25) - PI * 0.125);
	float oct_r = R_OCT * (cos(PI * 0.125) / cos(fold));
	float oct = 1.0 - smoothstep(0.0, 0.012, abs(r - oct_r));
	float a8 = (ang / TAU + 0.5) * 8.0;
	float d8 = min(fract(a8), 1.0 - fract(a8));
	float bag = (1.0 - smoothstep(0.0, 0.07, d8)) * smoothstep(R_OCT, R_OCT * 1.1, r) * (1.0 - smoothstep(R_OCT * 2.0, R_OCT * 2.3, r));
	float taiji = 1.0 - smoothstep(0.0, 0.012, abs(r - 0.055));
	float core = max(oct, max(bag, taiji));

	// --- Glyph/talisman ring (dashed) ---
	float gring = 1.0 - smoothstep(0.0, 0.02, abs(r - R_GLYPH));
	float dash = step(0.55, fract((ang / TAU + 0.5) * 44.0));
	float glyph = gring * dash;

	pattern = max(pattern, max(core, glyph));

	// The formation only manifests in the closing band (outside the clear center).
	float m = smoothstep(rc - 0.02, rc + 0.05, r);
	// Bright leading ring at the net's current boundary.
	float edge = 1.0 - smoothstep(0.0, 0.035, abs(r - rc));

	float a = pattern * m * 0.85 + edge + m * 0.05;
	vec3 col = mix(col_main.rgb, col_glow.rgb, clamp(pattern * 0.6 + edge, 0.0, 1.0));
	// Spirit-cyan turns to an urgent gold as the net nearly closes.
	float warn = smoothstep(0.7, 1.0, net);
	col = mix(col, vec3(1.0, 0.72, 0.28), warn * 0.55);

	COLOR = vec4(col, clamp(a, 0.0, 1.0) * 0.9);
}
"

var _rect: ColorRect
var _mat: ShaderMaterial

func _ready() -> void:
	add_to_group("net_overlay")
	layer = 0   # above the 3D world, below the HUD
	var sh := Shader.new()
	sh.code = SHADER_CODE
	_mat = ShaderMaterial.new()
	_mat.shader = sh
	_rect = ColorRect.new()
	_rect.anchor_right = 1.0
	_rect.anchor_bottom = 1.0
	_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rect.material = _mat
	add_child(_rect)
	get_viewport().size_changed.connect(_refresh_size)
	call_deferred("_refresh_size")
	on_net_changed(0.0)

func _refresh_size() -> void:
	if _mat != null and _rect != null:
		_mat.set_shader_parameter("screen_size", _rect.size)

## Set how far the net has closed (0..1).
func on_net_changed(net: float) -> void:
	if _mat != null:
		_mat.set_shader_parameter("net", net)
