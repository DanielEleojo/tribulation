extends CanvasLayer
## HUD: shows the running distance, and a death screen on game over.

@onready var distance_label: Label = $Distance
@onready var death_label: Label = $DeathLabel
@onready var qi_bar: ProgressBar = $QiBar
@onready var qi_label: Label = $QiLabel
@onready var flash_rect: ColorRect = $Flash
@onready var souls_label: Label = $SoulsLabel
@onready var title_root: Control = $Title
@onready var realm_label: Label = $RealmLabel
@onready var banner_label: Label = $Banner
@onready var shield_label: Label = $ShieldLabel

var player
var _souls: int = 0
var _best_li: int = 0
var _combo_label: Label
var _title_best: Label
var _pu_label: Label
var _glass_shader: Shader
var _glass_panels: Array = []

# Frosted-glass: blur the scene behind, tint, rounded corners, bright edge — iOS style.
const GLASS_SHADER := "
shader_type canvas_item;
render_mode blend_mix;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform vec2 rect_size = vec2(200.0, 60.0);
uniform float radius = 22.0;
uniform float blur = 2.2;
uniform vec4 tint : source_color = vec4(0.10, 0.12, 0.20, 0.42);
uniform vec4 border_col : source_color = vec4(1.0, 1.0, 1.0, 0.22);
float rrect(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - b + vec2(r);
	return min(max(q.x, q.y), 0.0) + length(max(q, vec2(0.0))) - r;
}
void fragment() {
	vec2 px = UV * rect_size;
	float d = rrect(px - rect_size * 0.5, rect_size * 0.5, radius);
	if (d > 0.0) {
		COLOR = vec4(0.0);
	} else {
		vec2 ts = 1.0 / vec2(textureSize(screen_tex, 0));
		vec3 col = vec3(0.0);
		for (int x = -2; x <= 2; x++) {
			for (int y = -2; y <= 2; y++) {
				col += texture(screen_tex, SCREEN_UV + vec2(float(x), float(y)) * ts * blur).rgb;
			}
		}
		col /= 25.0;
		vec3 g = mix(col, tint.rgb, tint.a);
		float edge = smoothstep(-3.0, -0.5, d);
		g = mix(g, border_col.rgb, edge * border_col.a);
		COLOR = vec4(g, clamp(-d, 0.0, 1.0));
	}
}
"

func _ready() -> void:
	add_to_group("hud")
	player = get_tree().get_first_node_in_group("player")
	death_label.visible = false
	flash_rect.color = Color(1, 1, 1, 0)
	title_root.visible = true   # start on the title screen
	banner_label.modulate.a = 0.0
	_build_glass()
	_style_widgets()
	_build_combo_and_best()
	get_viewport().size_changed.connect(_refresh_glass)
	call_deferred("_refresh_glass")

## Combo readout (center, under the realm) + best-li line on the title.
func _build_combo_and_best() -> void:
	_combo_label = Label.new()
	_combo_label.anchor_left = 0.5; _combo_label.anchor_right = 0.5
	_combo_label.offset_left = -180; _combo_label.offset_right = 180
	_combo_label.offset_top = 52; _combo_label.offset_bottom = 90
	_combo_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_combo_label.add_theme_font_size_override("font_size", 28)
	_combo_label.add_theme_color_override("font_color", Color(1.0, 0.82, 0.35))
	_combo_label.visible = false
	add_child(_combo_label)
	_title_best = Label.new()
	_title_best.anchor_left = 0.5; _title_best.anchor_right = 0.5
	_title_best.anchor_top = 0.5; _title_best.anchor_bottom = 0.5
	_title_best.offset_left = -300; _title_best.offset_right = 300
	_title_best.offset_top = 84; _title_best.offset_bottom = 120
	_title_best.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_title_best.add_theme_font_size_override("font_size", 24)
	_title_best.add_theme_color_override("font_color", Color(0.85, 0.9, 1.0))
	title_root.add_child(_title_best)
	# Active pills/talismans readout (centered, below the combo).
	_pu_label = Label.new()
	_pu_label.anchor_left = 0.5; _pu_label.anchor_right = 0.5
	_pu_label.offset_left = -300; _pu_label.offset_right = 300
	_pu_label.offset_top = 92; _pu_label.offset_bottom = 120
	_pu_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_pu_label.add_theme_font_size_override("font_size", 20)
	_pu_label.add_theme_color_override("font_color", Color(0.6, 0.95, 1.0))
	_pu_label.visible = false
	add_child(_pu_label)

## Active pills/talismans + countdown (hidden when none).
func set_powerups(text: String) -> void:
	if _pu_label == null:
		return
	_pu_label.text = text
	_pu_label.visible = text != ""

## Combo streak readout (hidden until 2+).
func on_combo_changed(c: int, mult: float) -> void:
	if _combo_label == null:
		return
	if c > 1:
		_combo_label.text = "DAO HEART  x%d   %.1f×" % [c, mult]
		_combo_label.visible = true
	else:
		_combo_label.visible = false

## Show/hide the Qi meter (Qi cultivation only begins at Golden Core).
func set_qi_visible(v: bool) -> void:
	qi_bar.visible = v
	qi_label.visible = v

## Best distance ever (shown on title + death).
func set_best(b: int) -> void:
	_best_li = b
	if _title_best != null:
		_title_best.text = ("Best: %d li" % b) if b > 0 else ""

## Build frosted-glass panels behind the HUD clusters.
func _build_glass() -> void:
	_glass_shader = Shader.new()
	_glass_shader.code = GLASS_SHADER
	# Left cluster (distance + Qi), center (realm), right (souls + shield).
	_glass(self, 12, 8, 372, 98, 0, 0, 0, 0, 24)
	_glass(self, -240, 6, 240, 54, 0.5, 0, 0.5, 0, 22)
	_glass(self, -252, 8, -12, 92, 1.0, 0, 1.0, 0, 22)
	# Death card.
	_glass(death_label, -28, -28, 28, 28, 0, 0, 1, 1, 28)
	# Title card behind the wordmark + hint.
	_glass(title_root, -380, -170, 380, 170, 0.5, 0.5, 0.5, 0.5, 32)

## Create one glass ColorRect and drop it behind its siblings.
func _glass(parent: Node, ol: float, ot: float, oraw: float, ob: float, al: float, at: float, ar: float, ab: float, radius: float) -> void:
	var r := ColorRect.new()
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.anchor_left = al; r.anchor_top = at; r.anchor_right = ar; r.anchor_bottom = ab
	r.offset_left = ol; r.offset_top = ot; r.offset_right = oraw; r.offset_bottom = ob
	var mat := ShaderMaterial.new()
	mat.shader = _glass_shader
	mat.set_shader_parameter("radius", radius)
	r.material = mat
	parent.add_child(r)
	parent.move_child(r, 0)   # behind the labels
	_glass_panels.append(r)

func _refresh_glass() -> void:
	for r in _glass_panels:
		if is_instance_valid(r) and r.material != null:
			r.material.set_shader_parameter("rect_size", r.size)

## Brighter, cleaner labels + a sleek Qi capsule.
func _style_widgets() -> void:
	var white := Color(0.95, 0.96, 1.0)
	for lbl in [distance_label, souls_label, shield_label, banner_label]:
		lbl.add_theme_color_override("font_color", white)
	realm_label.add_theme_color_override("font_color", Color(1.0, 0.92, 0.6))
	realm_label.add_theme_font_size_override("font_size", 22)   # room for "Realm · Nth Layer"
	realm_label.offset_left = -270
	realm_label.offset_right = 270
	# Qi capsule: translucent rounded track + glowing cyan fill.
	var track := StyleBoxFlat.new()
	track.bg_color = Color(1, 1, 1, 0.12)
	track.set_corner_radius_all(12)
	var fill := StyleBoxFlat.new()
	fill.bg_color = Color(0.35, 0.85, 1.0, 0.95)
	fill.set_corner_radius_all(12)
	qi_bar.add_theme_stylebox_override("background", track)
	qi_bar.add_theme_stylebox_override("fill", fill)

## Update the current cultivation realm name (top-center).
func set_realm(name: String) -> void:
	realm_label.text = name

## Show Iron Demon Body charges (hidden at zero).
func set_shields(n: int) -> void:
	if n > 0:
		shield_label.text = "Iron Body  " + "◆".repeat(n)
		shield_label.visible = true
	else:
		shield_label.visible = false

## Flash a big breakthrough banner that fades out.
func show_banner(name: String) -> void:
	banner_label.text = "⟡  %s  ⟡" % name
	banner_label.modulate.a = 1.0
	var tw := banner_label.create_tween()
	tw.tween_interval(0.8)
	tw.tween_property(banner_label, "modulate:a", 0.0, 1.0)

## Show/hide the title screen.
func show_title(v: bool) -> void:
	title_root.visible = v

## Brief full-screen color flash (gate feedback).
func flash(c: Color) -> void:
	flash_rect.color = Color(c.r, c.g, c.b, 0.45)
	var tw := flash_rect.create_tween()
	tw.tween_property(flash_rect, "color:a", 0.0, 0.4)

## Called by the game coordinator whenever Qi changes (and once at start).
func on_qi_changed(qi: float, qi_max: float) -> void:
	qi_bar.max_value = qi_max
	qi_bar.value = qi

## Called by the game coordinator whenever Demon Souls change (and once at start).
func on_souls_changed(souls: int) -> void:
	_souls = souls
	souls_label.text = "Stones: %d" % souls

func _process(_delta: float) -> void:
	if player == null:
		return
	distance_label.text = "%d li" % player.get_distance()

## Called by the game coordinator when the player dies.
func on_death() -> void:
	var dist := 0
	if player != null:
		dist = player.get_distance()
	death_label.text = "QI DEVIATION\n\n%d li traveled     Best: %d li\nSpirit Stones: %d\n\nEnter / tap to walk again\n[ Watch ad to continue — coming soon ]" % [dist, _best_li, _souls]
	death_label.visible = true
