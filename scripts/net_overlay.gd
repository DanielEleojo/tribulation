extends CanvasLayer
## The Heavenly Net: a glowing golden lattice that closes inward from the screen
## edges as the net tightens. net = 0 -> clear center; net = 1 -> fully netted (death).
## Driven by the game coordinator via on_net_changed().

const SHADER_CODE := "
shader_type canvas_item;
render_mode blend_mix;
uniform float net : hint_range(0.0, 1.0) = 0.0;
uniform vec4 net_color : source_color = vec4(0.95, 0.82, 0.35, 1.0);
void fragment() {
	vec2 uv = UV;
	// 0 at the nearest screen edge, 1 at the center.
	float e = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 2.0;
	// The net has crept in to depth `net` (soft inner edge).
	float band = 1.0 - smoothstep(net - 0.06, net, e);
	// Woven lattice: crossing line sets = a net mesh.
	vec2 g = uv * vec2(20.0, 36.0);
	vec2 f = abs(fract(g) - 0.5);
	float lx = 1.0 - smoothstep(0.40, 0.5, f.x);
	float ly = 1.0 - smoothstep(0.40, 0.5, f.y);
	float line = clamp(lx + ly, 0.0, 1.0);
	float a = band * (0.10 + 0.80 * line);
	COLOR = vec4(net_color.rgb, a * net_color.a);
}
"

var _rect: ColorRect
var _mat: ShaderMaterial

func _ready() -> void:
	add_to_group("net_overlay")
	layer = 0   # above the 3D world, below the HUD (HUD is layer 1)
	var sh := Shader.new()
	sh.code = SHADER_CODE
	_mat = ShaderMaterial.new()
	_mat.shader = sh
	_rect = ColorRect.new()
	_rect.anchor_right = 1.0
	_rect.anchor_bottom = 1.0
	_rect.offset_left = 0.0
	_rect.offset_top = 0.0
	_rect.offset_right = 0.0
	_rect.offset_bottom = 0.0
	_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rect.material = _mat
	add_child(_rect)
	on_net_changed(0.0)

## Set how far the net has closed (0..1).
func on_net_changed(net: float) -> void:
	if _mat != null:
		_mat.set_shader_parameter("net", net)
