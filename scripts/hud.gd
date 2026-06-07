extends CanvasLayer
## Modern UI: a clean main menu (Begin / Cultivation / Settings), an in-run HUD
## (Qi counter, realm + layer, burst gauge, trials, power-ups), a pause panel, a
## settings panel (music / effects / mute / reset), and a death card. All styled
## in code with a single jade-Qi palette so it reads as one cohesive app.
##
## Public API (called by game.gd) is preserved: on_death, on_qi_changed,
## on_souls_changed, on_combo_changed, set_realm, set_best, set_qi_visible,
## set_shields, show_banner, show_title, flash, set_tribulation, set_powerups,
## set_trials.

# Scene nodes we repurpose as data sinks.
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

# ---- palette (one cohesive jade-Qi theme) ----
const ACCENT      := Color(0.36, 0.86, 0.78)   # Qi jade-cyan
const ACCENT_DK   := Color(0.18, 0.52, 0.48)
const GOLD        := Color(1.00, 0.84, 0.46)    # realm / breakthrough
const INK         := Color(0.04, 0.05, 0.07)    # text on accent
const TEXT        := Color(0.93, 0.95, 1.00)
const DIM         := Color(0.60, 0.66, 0.78)
const DANGER      := Color(0.96, 0.45, 0.45)
const PANEL       := Color(0.07, 0.08, 0.11, 0.86)
const PANEL_SOLID := Color(0.06, 0.07, 0.10, 0.98)
const STROKE      := Color(1, 1, 1, 0.10)

var player
var _souls: int = 0
var _best_li: int = 0
var _game_ref

# built widgets
var _combo_label: Label
var _trials_label: Label
var _pu_label: Label
var _trib_label: Label
var _qi_pill: Panel
var _dist_pill: Panel
var _burst_box: Control
var _pause_btn: Button
# overlays
var _menu_box: VBoxContainer
var _menu_realm: Label
var _menu_best: Label
var _shop_root: Control
var _shop_balance: Label
var _shop_buttons: Dictionary = {}
var _settings_root: Control
var _music_slider: HSlider
var _sfx_slider: HSlider
var _mute_chk: CheckButton
var _reset_btn: Button
var _reset_armed: bool = false
var _pause_root: Control
var _death_root: Control
var _death_title: Label
var _death_stats: Label
var _paused: bool = false


func _game():
	if _game_ref == null:
		_game_ref = get_tree().get_first_node_in_group("game")
	return _game_ref


func _ready() -> void:
	add_to_group("hud")
	process_mode = Node.PROCESS_MODE_ALWAYS   # so pause toggling keeps working
	player = get_tree().get_first_node_in_group("player")
	flash_rect.color = Color(1, 1, 1, 0)
	banner_label.modulate.a = 0.0
	death_label.visible = false
	_style_hud()
	_build_menu()
	_build_pause()
	_build_settings()
	_build_death()
	call_deferred("_build_shop")


# ================================================================ style helpers
func _sb(bg: Color, radius: int, border: Color = STROKE, bw: int = 1) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.set_corner_radius_all(radius)
	if bw > 0:
		s.set_border_width_all(bw)
		s.border_color = border
	s.content_margin_left = 16; s.content_margin_right = 16
	s.content_margin_top = 8; s.content_margin_bottom = 8
	return s


## A rounded button. kind: "primary" (accent fill), "ghost" (outline), "danger".
func _btn(text: String, kind: String = "ghost", font_size: int = 26) -> Button:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	b.add_theme_font_size_override("font_size", font_size)
	var normal: StyleBoxFlat
	var hover: StyleBoxFlat
	var pressed: StyleBoxFlat
	match kind:
		"primary":
			normal = _sb(ACCENT, 16, ACCENT, 0)
			hover = _sb(ACCENT.lightened(0.12), 16, ACCENT, 0)
			pressed = _sb(ACCENT_DK, 16, ACCENT, 0)
			b.add_theme_color_override("font_color", INK)
			b.add_theme_color_override("font_hover_color", INK)
			b.add_theme_color_override("font_pressed_color", INK)
		"danger":
			normal = _sb(Color(0.30, 0.10, 0.12, 0.85), 14, DANGER, 1)
			hover = _sb(Color(0.42, 0.12, 0.14, 0.95), 14, DANGER, 1)
			pressed = _sb(Color(0.22, 0.08, 0.10, 0.95), 14, DANGER, 1)
			b.add_theme_color_override("font_color", DANGER)
			b.add_theme_color_override("font_hover_color", Color(1, 0.7, 0.7))
		_:
			normal = _sb(Color(1, 1, 1, 0.06), 14, STROKE, 1)
			hover = _sb(Color(1, 1, 1, 0.13), 14, Color(1, 1, 1, 0.22), 1)
			pressed = _sb(Color(1, 1, 1, 0.04), 14, STROKE, 1)
			b.add_theme_color_override("font_color", TEXT)
			b.add_theme_color_override("font_hover_color", ACCENT)
	for sbn in ["normal", "hover", "pressed", "focus"]:
		b.add_theme_stylebox_override(sbn, normal if sbn != "hover" else hover)
	b.add_theme_stylebox_override("pressed", pressed)
	b.add_theme_stylebox_override("disabled", _sb(Color(1, 1, 1, 0.03), 14, STROKE, 1))
	b.add_theme_color_override("font_disabled_color", DIM)
	return b


func _label(text: String, size: int, col: Color, align: int = HORIZONTAL_ALIGNMENT_LEFT) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	l.horizontal_alignment = align
	return l


func _full_overlay(dim: float) -> Control:
	var c := Control.new()
	c.set_anchors_preset(Control.PRESET_FULL_RECT)
	c.mouse_filter = Control.MOUSE_FILTER_STOP   # swallow taps so they don't reach gameplay
	c.process_mode = Node.PROCESS_MODE_ALWAYS
	var bg := ColorRect.new()
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.color = Color(0.02, 0.03, 0.05, dim)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	c.add_child(bg)
	return c


func _card(parent: Control, w: float, h: float) -> Panel:
	var p := Panel.new()
	p.add_theme_stylebox_override("panel", _sb(PANEL_SOLID, 24, STROKE, 1))
	p.set_anchors_preset(Control.PRESET_CENTER)
	p.offset_left = -w * 0.5; p.offset_right = w * 0.5
	p.offset_top = -h * 0.5; p.offset_bottom = h * 0.5
	parent.add_child(p)
	return p


# ================================================================ in-run HUD
func _style_hud() -> void:
	# Qi counter pill (top-left).
	_qi_pill = _hud_pill(20, 40, 210, 56)
	souls_label.add_theme_font_size_override("font_size", 28)
	souls_label.add_theme_color_override("font_color", ACCENT)
	_place(souls_label, _qi_pill, 18, 0, -14, 0, HORIZONTAL_ALIGNMENT_LEFT)

	# Distance pill (top-right, left of the pause button).
	_dist_pill = _hud_pill_r(86, 40, 250, 56)
	distance_label.add_theme_font_size_override("font_size", 26)
	distance_label.add_theme_color_override("font_color", TEXT)
	_place(distance_label, _dist_pill, 16, 0, -16, 0, HORIZONTAL_ALIGNMENT_RIGHT)

	# Pause / settings button (top-right corner).
	_pause_btn = _btn("II", "ghost", 24)
	_pause_btn.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_pause_btn.offset_left = -76; _pause_btn.offset_right = -20
	_pause_btn.offset_top = 40; _pause_btn.offset_bottom = 96
	_pause_btn.pressed.connect(_toggle_pause)
	_pause_btn.visible = false
	add_child(_pause_btn)

	# Realm + layer (top-center, gold).
	realm_label.add_theme_color_override("font_color", GOLD)
	realm_label.add_theme_font_size_override("font_size", 24)
	realm_label.offset_left = -300; realm_label.offset_right = 300
	realm_label.offset_top = 104; realm_label.offset_bottom = 138

	# Burst gauge (the qi meter) under the realm — hidden until Golden Core.
	_burst_box = Control.new()
	_burst_box.set_anchors_preset(Control.PRESET_TOP_WIDE)
	_burst_box.offset_left = 0; _burst_box.offset_right = 0
	_burst_box.offset_top = 150; _burst_box.offset_bottom = 184
	_burst_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_burst_box)
	qi_label.reparent(_burst_box)
	qi_label.text = "BURST"
	qi_label.add_theme_font_size_override("font_size", 16)
	qi_label.add_theme_color_override("font_color", DIM)
	qi_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
	qi_label.offset_left = -130; qi_label.offset_right = -78
	qi_label.offset_top = 4; qi_label.offset_bottom = 26
	qi_bar.reparent(_burst_box)
	qi_bar.set_anchors_preset(Control.PRESET_CENTER_TOP)
	qi_bar.offset_left = -70; qi_bar.offset_right = 150
	qi_bar.offset_top = 6; qi_bar.offset_bottom = 24
	qi_bar.show_percentage = false
	var track := StyleBoxFlat.new()
	track.bg_color = Color(1, 1, 1, 0.10); track.set_corner_radius_all(9)
	var fill := StyleBoxFlat.new()
	fill.bg_color = ACCENT; fill.set_corner_radius_all(9)
	qi_bar.add_theme_stylebox_override("background", track)
	qi_bar.add_theme_stylebox_override("fill", fill)
	_burst_box.visible = false

	# Combo (Dao Heart), trials, power-ups.
	_combo_label = _label("", 24, GOLD, HORIZONTAL_ALIGNMENT_CENTER)
	_combo_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_combo_label.offset_left = -200; _combo_label.offset_right = 200
	_combo_label.offset_top = 190; _combo_label.offset_bottom = 222
	_combo_label.visible = false
	add_child(_combo_label)

	_trials_label = _label("", 17, Color(0.88, 0.91, 1.0), HORIZONTAL_ALIGNMENT_LEFT)
	_trials_label.offset_left = 22; _trials_label.offset_right = 420
	_trials_label.offset_top = 240; _trials_label.offset_bottom = 380
	add_child(_trials_label)

	_pu_label = _label("", 18, Color(0.6, 0.95, 1.0), HORIZONTAL_ALIGNMENT_CENTER)
	_pu_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_pu_label.offset_left = -300; _pu_label.offset_right = 300
	_pu_label.offset_top = 224; _pu_label.offset_bottom = 252
	_pu_label.visible = false
	add_child(_pu_label)

	# Shield readout sits just under the Qi pill.
	shield_label.add_theme_font_size_override("font_size", 18)
	shield_label.add_theme_color_override("font_color", Color(0.85, 0.88, 1.0))
	shield_label.set_anchors_preset(Control.PRESET_TOP_LEFT)
	shield_label.offset_left = 24; shield_label.offset_right = 280
	shield_label.offset_top = 102; shield_label.offset_bottom = 128

	# Banner styling.
	banner_label.add_theme_color_override("font_color", GOLD)
	banner_label.add_theme_font_size_override("font_size", 46)


func _hud_pill(ox: float, oy: float, w: float, h: float) -> Panel:
	var p := Panel.new()
	p.add_theme_stylebox_override("panel", _sb(PANEL, 18, STROKE, 1))
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.set_anchors_preset(Control.PRESET_TOP_LEFT)
	p.offset_left = ox; p.offset_top = oy
	p.offset_right = ox + w; p.offset_bottom = oy + h
	add_child(p)
	return p


func _hud_pill_r(margin_top: float, _unused: float, w: float, h: float) -> Panel:
	var p := Panel.new()
	p.add_theme_stylebox_override("panel", _sb(PANEL, 18, STROKE, 1))
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	p.offset_right = -86; p.offset_left = -86 - w
	p.offset_top = margin_top; p.offset_bottom = margin_top + h
	add_child(p)
	return p


## Stretch a label to fill a pill, with padding.
func _place(lbl: Label, pill: Panel, l: float, t: float, r: float, b: float, align: int) -> void:
	if lbl.get_parent() != pill:
		lbl.reparent(pill)
	lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
	lbl.offset_left = l; lbl.offset_top = t; lbl.offset_right = r; lbl.offset_bottom = b
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	lbl.horizontal_alignment = align


# ================================================================ main menu
func _build_menu() -> void:
	# Restyle the scene's title backdrop + wordmark.
	var backdrop := title_root.get_node_or_null("Backdrop")
	if backdrop is ColorRect:
		backdrop.color = Color(0.03, 0.04, 0.06, 0.92)
	var name_lbl := title_root.get_node_or_null("Name")
	var hint_lbl := title_root.get_node_or_null("Hint")
	if name_lbl is Label:
		name_lbl.add_theme_color_override("font_color", TEXT)
		name_lbl.add_theme_font_size_override("font_size", 64)
		name_lbl.offset_top = -300; name_lbl.offset_bottom = -210
	if hint_lbl is Label:
		hint_lbl.text = "The Cultivator's Road"
		hint_lbl.add_theme_color_override("font_color", ACCENT)
		hint_lbl.add_theme_font_size_override("font_size", 24)
		hint_lbl.offset_top = -206; hint_lbl.offset_bottom = -170

	_menu_box = VBoxContainer.new()
	_menu_box.set_anchors_preset(Control.PRESET_CENTER)
	_menu_box.offset_left = -220; _menu_box.offset_right = 220
	_menu_box.offset_top = -120; _menu_box.offset_bottom = 300
	_menu_box.add_theme_constant_override("separation", 16)
	title_root.add_child(_menu_box)

	_menu_realm = _label("", 22, GOLD, HORIZONTAL_ALIGNMENT_CENTER)
	_menu_best = _label("", 20, DIM, HORIZONTAL_ALIGNMENT_CENTER)
	_menu_box.add_child(_menu_realm)
	_menu_box.add_child(_menu_best)

	var spacer := Control.new(); spacer.custom_minimum_size = Vector2(0, 8)
	_menu_box.add_child(spacer)

	var begin := _btn("Begin Cultivation", "primary", 32)
	begin.custom_minimum_size = Vector2(440, 64)
	begin.pressed.connect(_on_begin)
	_menu_box.add_child(begin)

	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 16)
	_menu_box.add_child(row)
	var cult := _btn("Cultivation", "ghost", 26)
	cult.custom_minimum_size = Vector2(212, 56)
	cult.pressed.connect(func(): _show_overlay(_shop_root, true); _refresh_shop())
	row.add_child(cult)
	var sett := _btn("Settings", "ghost", 26)
	sett.custom_minimum_size = Vector2(212, 56)
	sett.pressed.connect(func(): _show_overlay(_settings_root, true); _refresh_settings())
	row.add_child(sett)


func _on_begin() -> void:
	var g = _game()
	if g != null:
		g.start_game()


# ================================================================ cultivation shop
func _build_shop() -> void:
	var g = _game()
	if g == null or not ("UPGRADES" in g):
		return
	_shop_root = _full_overlay(0.78)
	add_child(_shop_root)
	var card := _card(_shop_root, 600, 700)
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.offset_left = 28; v.offset_right = -28; v.offset_top = 28; v.offset_bottom = -28
	v.add_theme_constant_override("separation", 12)
	card.add_child(v)
	v.add_child(_label("Cultivation", 34, TEXT, HORIZONTAL_ALIGNMENT_CENTER))
	_shop_balance = _label("", 22, ACCENT, HORIZONTAL_ALIGNMENT_CENTER)
	v.add_child(_shop_balance)
	v.add_child(_label("Refine your dao with gathered Qi.", 17, DIM, HORIZONTAL_ALIGNMENT_CENTER))
	var sp := Control.new(); sp.custom_minimum_size = Vector2(0, 6); v.add_child(sp)
	for id in g.UPGRADES.keys():
		var b := _btn("", "ghost", 20)
		b.custom_minimum_size = Vector2(0, 72)
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		b.pressed.connect(_on_buy.bind(id))
		_shop_buttons[id] = b
		v.add_child(b)
	var sp2 := Control.new(); sp2.size_flags_vertical = Control.SIZE_EXPAND_FILL; v.add_child(sp2)
	var back := _btn("Back", "ghost", 26)
	back.custom_minimum_size = Vector2(0, 56)
	back.pressed.connect(func(): _show_overlay(_shop_root, false))
	v.add_child(back)
	_show_overlay(_shop_root, false)


func _refresh_shop() -> void:
	var g = _game()
	if g == null or _shop_root == null:
		return
	_shop_balance.text = "Qi:  %d" % g.balance()
	for id in _shop_buttons:
		var d = g.UPGRADES[id]
		var b: Button = _shop_buttons[id]
		var lv: int = g.upgrade_level(id)
		var mx: int = int(d["max"])
		if g.upgrade_maxed(id):
			b.text = "%s   ·   Lv %d/%d   ·   ✦ MAX" % [d["name"], lv, mx]
			b.disabled = true
		else:
			var cost: int = g.upgrade_cost(id)
			b.text = "%s   Lv %d/%d\n%s   —   Cultivate (%d Qi)" % [d["name"], lv, mx, d["desc"], cost]
			b.disabled = g.balance() < cost


func _on_buy(id: String) -> void:
	var g = _game()
	if g != null and g.buy_upgrade(id):
		_refresh_shop()


# ================================================================ settings
func _build_settings() -> void:
	_settings_root = _full_overlay(0.82)
	add_child(_settings_root)
	var card := _card(_settings_root, 580, 620)
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.offset_left = 32; v.offset_right = -32; v.offset_top = 30; v.offset_bottom = -30
	v.add_theme_constant_override("separation", 18)
	card.add_child(v)
	v.add_child(_label("Settings", 34, TEXT, HORIZONTAL_ALIGNMENT_CENTER))
	var sp := Control.new(); sp.custom_minimum_size = Vector2(0, 6); v.add_child(sp)

	v.add_child(_label("Music", 22, DIM))
	_music_slider = _slider()
	_music_slider.value_changed.connect(func(val): if _game(): _game().set_music_vol(val))
	v.add_child(_music_slider)

	v.add_child(_label("Effects", 22, DIM))
	_sfx_slider = _slider()
	_sfx_slider.value_changed.connect(func(val): if _game(): _game().set_sfx_vol(val))
	v.add_child(_sfx_slider)

	_mute_chk = CheckButton.new()
	_mute_chk.text = "Mute all"
	_mute_chk.add_theme_font_size_override("font_size", 22)
	_mute_chk.add_theme_color_override("font_color", TEXT)
	_mute_chk.toggled.connect(func(on): if _game(): _game().set_muted(on))
	v.add_child(_mute_chk)

	var sp2 := Control.new(); sp2.size_flags_vertical = Control.SIZE_EXPAND_FILL; v.add_child(sp2)

	_reset_btn = _btn("Reset Cultivation", "danger", 22)
	_reset_btn.custom_minimum_size = Vector2(0, 54)
	_reset_btn.pressed.connect(_on_reset)
	v.add_child(_reset_btn)

	var back := _btn("Back", "ghost", 26)
	back.custom_minimum_size = Vector2(0, 56)
	back.pressed.connect(func(): _reset_armed = false; _reset_btn.text = "Reset Cultivation"; _show_overlay(_settings_root, false))
	v.add_child(back)
	_show_overlay(_settings_root, false)


func _slider() -> HSlider:
	var s := HSlider.new()
	s.min_value = 0.0; s.max_value = 1.0; s.step = 0.05
	s.custom_minimum_size = Vector2(0, 28)
	var track := StyleBoxFlat.new()
	track.bg_color = Color(1, 1, 1, 0.12); track.set_corner_radius_all(7)
	track.content_margin_top = 6; track.content_margin_bottom = 6
	var fill := StyleBoxFlat.new()
	fill.bg_color = ACCENT; fill.set_corner_radius_all(7)
	fill.content_margin_top = 6; fill.content_margin_bottom = 6
	s.add_theme_stylebox_override("slider", track)
	s.add_theme_stylebox_override("grabber_area", fill)
	s.add_theme_stylebox_override("grabber_area_highlight", fill)
	return s


func _refresh_settings() -> void:
	var g = _game()
	if g == null:
		return
	_music_slider.set_value_no_signal(g.get_music_vol())
	_sfx_slider.set_value_no_signal(g.get_sfx_vol())
	_mute_chk.set_pressed_no_signal(g.is_muted())


func _on_reset() -> void:
	if not _reset_armed:
		_reset_armed = true
		_reset_btn.text = "Tap again to erase ALL progress"
		return
	var g = _game()
	if g != null:
		g.reset_cultivation()


# ================================================================ pause
func _build_pause() -> void:
	_pause_root = _full_overlay(0.72)
	add_child(_pause_root)
	var card := _card(_pause_root, 480, 420)
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.offset_left = 32; v.offset_right = -32; v.offset_top = 30; v.offset_bottom = -30
	v.add_theme_constant_override("separation", 16)
	card.add_child(v)
	v.add_child(_label("Paused", 34, TEXT, HORIZONTAL_ALIGNMENT_CENTER))
	var sp := Control.new(); sp.size_flags_vertical = Control.SIZE_EXPAND_FILL; v.add_child(sp)
	var resume := _btn("Resume", "primary", 28)
	resume.custom_minimum_size = Vector2(0, 60); resume.pressed.connect(_toggle_pause)
	v.add_child(resume)
	var sett := _btn("Settings", "ghost", 26)
	sett.custom_minimum_size = Vector2(0, 56)
	sett.pressed.connect(func(): _show_overlay(_settings_root, true); _refresh_settings())
	v.add_child(sett)
	var quit := _btn("Abandon Run", "ghost", 26)
	quit.custom_minimum_size = Vector2(0, 56)
	quit.pressed.connect(_quit_to_menu)
	v.add_child(quit)
	_show_overlay(_pause_root, false)


func _toggle_pause() -> void:
	var g = _game()
	if g == null or not g.started or g.is_dead:
		return
	# If settings is on top, the cancel just closes settings.
	if _settings_root.visible:
		_show_overlay(_settings_root, false)
		return
	_paused = not _paused
	get_tree().paused = _paused
	_show_overlay(_pause_root, _paused)


func _quit_to_menu() -> void:
	get_tree().paused = false
	_paused = false
	var g = _game()
	if g != null:
		g.restart()   # reloads the scene -> back to the title


# ================================================================ death card
func _build_death() -> void:
	_death_root = _full_overlay(0.80)
	add_child(_death_root)
	var card := _card(_death_root, 560, 480)
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.offset_left = 32; v.offset_right = -32; v.offset_top = 34; v.offset_bottom = -30
	v.add_theme_constant_override("separation", 14)
	card.add_child(v)
	_death_title = _label("QI DEVIATION", 38, DANGER, HORIZONTAL_ALIGNMENT_CENTER)
	v.add_child(_death_title)
	_death_stats = _label("", 22, TEXT, HORIZONTAL_ALIGNMENT_CENTER)
	_death_stats.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	v.add_child(_death_stats)
	var sp := Control.new(); sp.size_flags_vertical = Control.SIZE_EXPAND_FILL; v.add_child(sp)
	var again := _btn("Walk the Road Again", "primary", 28)
	again.custom_minimum_size = Vector2(0, 62)
	again.pressed.connect(func(): if _game(): _game().restart())
	v.add_child(again)
	var ad := _label("Watch ad to continue — coming soon", 16, DIM, HORIZONTAL_ALIGNMENT_CENTER)
	v.add_child(ad)
	_show_overlay(_death_root, false)


# ================================================================ overlay helper
func _show_overlay(root: Control, v: bool) -> void:
	if root != null:
		root.visible = v


# ================================================================ public API
func _input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		var g = _game()
		if _settings_root != null and _settings_root.visible:
			_show_overlay(_settings_root, false)
		elif _shop_root != null and _shop_root.visible:
			_show_overlay(_shop_root, false)
		elif g != null and g.started and not g.is_dead:
			_toggle_pause()
		get_viewport().set_input_as_handled()


func show_title(v: bool) -> void:
	title_root.visible = v
	if _pause_btn != null:
		_pause_btn.visible = not v   # pause only during a run
	if v:
		_refresh_menu()


func _refresh_menu() -> void:
	if _menu_realm != null:
		_menu_realm.text = _realm_text
	if _menu_best != null:
		_menu_best.text = ("Best:  %d li" % _best_li) if _best_li > 0 else "A long road awaits"


var _realm_text: String = ""

func set_realm(name: String) -> void:
	realm_label.text = name
	_realm_text = name
	if _menu_realm != null and title_root.visible:
		_menu_realm.text = name


func set_best(b: int) -> void:
	_best_li = b
	_refresh_menu()


func set_qi_visible(v: bool) -> void:
	if _burst_box != null:
		_burst_box.visible = v


func set_shields(n: int) -> void:
	if n > 0:
		shield_label.text = "Iron Body  " + "◆".repeat(n)
		shield_label.visible = true
	else:
		shield_label.visible = false


func set_powerups(text: String) -> void:
	if _pu_label == null:
		return
	_pu_label.text = text
	_pu_label.visible = text != ""


func set_trials(text: String) -> void:
	if _trials_label != null:
		_trials_label.text = text


func on_combo_changed(c: int, mult: float) -> void:
	if _combo_label == null:
		return
	if c > 1:
		_combo_label.text = "DAO HEART  x%d   %.1f×" % [c, mult]
		_combo_label.visible = true
	else:
		_combo_label.visible = false


func set_tribulation(active: bool, t: float) -> void:
	if _trib_label == null:
		_trib_label = _label("", 32, GOLD, HORIZONTAL_ALIGNMENT_CENTER)
		_trib_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
		_trib_label.offset_left = -340; _trib_label.offset_right = 340
		_trib_label.offset_top = 300; _trib_label.offset_bottom = 380
		_trib_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		add_child(_trib_label)
	_trib_label.visible = active
	if active:
		_trib_label.text = "⚡ HEAVENLY TRIBULATION ⚡\nEndure  %ds" % int(ceil(t))


func show_banner(name: String) -> void:
	banner_label.text = "⟡  %s  ⟡" % name
	banner_label.modulate.a = 1.0
	var tw := banner_label.create_tween()
	tw.tween_interval(0.8)
	tw.tween_property(banner_label, "modulate:a", 0.0, 1.0)


func flash(c: Color) -> void:
	flash_rect.color = Color(c.r, c.g, c.b, 0.40)
	var tw := flash_rect.create_tween()
	tw.tween_property(flash_rect, "color:a", 0.0, 0.4)


func on_qi_changed(qi: float, qi_max: float) -> void:
	qi_bar.max_value = qi_max
	qi_bar.value = qi


func on_souls_changed(souls: int) -> void:
	_souls = souls
	souls_label.text = "◈ %d" % souls


func _process(_delta: float) -> void:
	if player != null:
		distance_label.text = "%d li" % player.get_distance()


func on_death() -> void:
	var dist := 0
	if player != null:
		dist = player.get_distance()
	if _pause_btn != null:
		_pause_btn.visible = false
	_death_stats.text = "%s\n\n%d li traveled     Best: %d li\n+%d Qi gathered this run" % [_realm_text, dist, _best_li, _souls]
	_show_overlay(_death_root, true)
