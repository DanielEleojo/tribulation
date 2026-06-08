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
var _daily_btn: Button
var _journal_root: Control
var _journal_stats: Label
var _journal_list: VBoxContainer
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

# ---- onboarding tutorial ----
var _tut_root: Control
var _tut_count: Label
var _tut_title: Label
var _tut_body: Label
var _tut_cont: Button
var _tut_skip: Button
var _tut_active: bool = false
var _tut_step: int = 0
var _tut_then_start: bool = false
var _tut_touch_start: Vector2 = Vector2.ZERO
var _tut_touching: bool = false
const _TUT_STEPS := [
	{"type": "info", "title": "The Cultivator's Road",
	 "body": "You walk the endless road of cultivation.\nEndure its trials, gather Qi, and ascend realm by realm.",
	 "btn": "Continue"},
	{"type": "gesture", "need": ["left", "right"], "title": "Change Lane",
	 "body": "Swipe LEFT or RIGHT   ( ◀ ▶ / A · D )\nto step between the three lanes.\n\nTry it now."},
	{"type": "gesture", "need": ["up"], "title": "Leap",
	 "body": "Swipe UP   ( ▲ / Space )\nto leap over Stone Wards.\n\nTry it now."},
	{"type": "gesture", "need": ["down"], "title": "Slide",
	 "body": "Swipe DOWN   ( ▼ / S )\nto slide under Spirit Barriers.\n\nTry it now."},
	{"type": "info", "title": "Ascend",
	 "body": "New arts awaken as you ascend — Qi Leap, sword-qi, even flight. Your major realm is saved; a fall costs only this layer's progress.",
	 "btn": "Begin Cultivation"},
]


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
	_build_tutorial()
	call_deferred("_build_shop")
	call_deferred("_build_journal")


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

	_daily_btn = _btn("", "primary", 24)
	_daily_btn.custom_minimum_size = Vector2(440, 54)
	_daily_btn.pressed.connect(_on_daily)
	_menu_box.add_child(_daily_btn)

	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 12)
	_menu_box.add_child(row)
	var cult := _btn("Cultivation", "ghost", 22)
	cult.custom_minimum_size = Vector2(140, 56)
	cult.pressed.connect(func(): _show_overlay(_shop_root, true); _refresh_shop())
	row.add_child(cult)
	var jour := _btn("Journal", "ghost", 22)
	jour.custom_minimum_size = Vector2(140, 56)
	jour.pressed.connect(func(): _show_overlay(_journal_root, true); _refresh_journal())
	row.add_child(jour)
	var sett := _btn("Settings", "ghost", 22)
	sett.custom_minimum_size = Vector2(140, 56)
	sett.pressed.connect(func(): _show_overlay(_settings_root, true); _refresh_settings())
	row.add_child(sett)


func _on_daily() -> void:
	var g = _game()
	if g == null or not g.has_method("claim_daily"):
		return
	var r = g.claim_daily()
	if r.is_empty():
		return
	_sfx("breakthrough")
	show_banner("Daily Qi  +%d   ·   Day %d streak" % [int(r["reward"]), int(r["streak"])])
	_refresh_menu()


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


# ================================================================ cultivation journal
func _build_journal() -> void:
	var g = _game()
	if g == null or not ("ACHIEVEMENTS" in g):
		return
	_journal_root = _full_overlay(0.84)
	add_child(_journal_root)
	var card := _card(_journal_root, 620, 780)
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.offset_left = 28; v.offset_right = -28; v.offset_top = 28; v.offset_bottom = -26
	v.add_theme_constant_override("separation", 12)
	card.add_child(v)
	v.add_child(_label("Cultivation Journal", 32, TEXT, HORIZONTAL_ALIGNMENT_CENTER))
	_journal_stats = _label("", 18, ACCENT, HORIZONTAL_ALIGNMENT_CENTER)
	_journal_stats.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	v.add_child(_journal_stats)
	v.add_child(_label("— Achievements —", 18, DIM, HORIZONTAL_ALIGNMENT_CENTER))
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	v.add_child(scroll)
	_journal_list = VBoxContainer.new()
	_journal_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_journal_list.custom_minimum_size = Vector2(540, 0)
	_journal_list.add_theme_constant_override("separation", 8)
	scroll.add_child(_journal_list)
	var back := _btn("Back", "ghost", 26)
	back.custom_minimum_size = Vector2(0, 56)
	back.pressed.connect(func(): _show_overlay(_journal_root, false))
	v.add_child(back)
	_show_overlay(_journal_root, false)


func _refresh_journal() -> void:
	var g = _game()
	if g == null or _journal_root == null:
		return
	var s = g.get_stats()
	_journal_stats.text = "Realm: %s\nBest: %d li     ·     Runs: %d\nFoes slain: %d     ·     Tribulations: %d\nLifetime Qi: %d" % [
		String(s["realm_name"]), int(s["best"]), int(s["runs"]), int(s["foes"]), int(s["tribs"]), int(s["total"])]
	for ch in _journal_list.get_children():
		ch.queue_free()
	for a in g.ACHIEVEMENTS:
		var got: bool = g.is_ach_unlocked(String(a["id"]))
		var mark := "✓  " if got else "○  "
		var line := _label("%s%s — %s" % [mark, String(a["name"]), String(a["desc"])], 17, GOLD if got else DIM)
		line.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_journal_list.add_child(line)


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

	var howto := _btn("How to Play", "ghost", 22)
	howto.custom_minimum_size = Vector2(0, 54)
	howto.pressed.connect(func(): _show_overlay(_settings_root, false); begin_tutorial(false))
	v.add_child(howto)

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


# ================================================================ onboarding tutorial
func _sfx(n: String) -> void:
	var s = get_tree().get_first_node_in_group("sound")
	if s != null:
		s.play(n)


func _build_tutorial() -> void:
	# STOP root so it owns input; we read swipes off its own gui_input (no dependency
	# on the gameplay swipe detector), and poll keys in _process.
	_tut_root = Control.new()
	_tut_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	_tut_root.mouse_filter = Control.MOUSE_FILTER_STOP
	_tut_root.process_mode = Node.PROCESS_MODE_ALWAYS
	_tut_root.gui_input.connect(_tut_on_gui)
	var bg := ColorRect.new()
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.color = Color(0.02, 0.03, 0.05, 0.90)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_tut_root.add_child(bg)
	add_child(_tut_root)
	var card := _card(_tut_root, 600, 470)
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE   # let practice swipes cross the card
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.offset_left = 32; v.offset_right = -32; v.offset_top = 28; v.offset_bottom = -26
	v.add_theme_constant_override("separation", 14)
	v.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(v)
	_tut_count = _label("", 16, ACCENT, HORIZONTAL_ALIGNMENT_CENTER)
	v.add_child(_tut_count)
	_tut_title = _label("", 32, TEXT, HORIZONTAL_ALIGNMENT_CENTER)
	v.add_child(_tut_title)
	_tut_body = _label("", 21, DIM, HORIZONTAL_ALIGNMENT_CENTER)
	_tut_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_tut_body.custom_minimum_size = Vector2(0, 150)
	_tut_body.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	v.add_child(_tut_body)
	var sp := Control.new(); sp.size_flags_vertical = Control.SIZE_EXPAND_FILL
	sp.mouse_filter = Control.MOUSE_FILTER_IGNORE; v.add_child(sp)
	_tut_cont = _btn("Continue", "primary", 26)
	_tut_cont.custom_minimum_size = Vector2(0, 58)
	_tut_cont.pressed.connect(_tut_advance)
	v.add_child(_tut_cont)
	_tut_skip = _btn("Skip", "ghost", 20)
	_tut_skip.custom_minimum_size = Vector2(0, 42)
	_tut_skip.pressed.connect(_tut_skip_all)
	v.add_child(_tut_skip)
	_tut_root.visible = false


## then_start=true: first-run flow (start the game when done). false: replay from Settings.
func begin_tutorial(then_start: bool = true) -> void:
	if _tut_active:
		return
	_tut_active = true
	_tut_then_start = then_start
	_tut_step = 0
	_show_overlay(_tut_root, true)
	_tut_render()


func _tut_render() -> void:
	var s = _TUT_STEPS[_tut_step]
	_tut_count.text = "%d / %d" % [_tut_step + 1, _TUT_STEPS.size()]
	_tut_title.text = String(s["title"])
	_tut_body.text = String(s["body"])
	var is_info: bool = s["type"] == "info"
	_tut_cont.visible = is_info
	if is_info:
		_tut_cont.text = String(s.get("btn", "Continue"))
	_tut_skip.visible = _tut_step < _TUT_STEPS.size() - 1


func _tut_next() -> void:
	if _tut_step >= _TUT_STEPS.size() - 1:
		_tut_finish()
	else:
		_tut_step += 1
		_tut_render()


func _tut_advance() -> void:        # info-step "Continue/Begin" button
	_sfx("orb")
	_tut_next()


func _tut_gesture(kind: String) -> void:
	if not _tut_active:
		return
	var s = _TUT_STEPS[_tut_step]
	if s["type"] != "gesture":
		return
	if kind in s["need"]:
		_sfx("orb")
		flash(ACCENT)
		_tut_next()


func _tut_skip_all() -> void:
	_tut_finish()


func _tut_finish() -> void:
	_tut_active = false
	_show_overlay(_tut_root, false)
	var g = _game()
	if g != null and g.has_method("mark_tutorial_done"):
		g.mark_tutorial_done()
	if _tut_then_start and g != null:
		g.start_game()


## Read swipes directly off the overlay (works with emulate_touch_from_mouse too).
func _tut_on_gui(event: InputEvent) -> void:
	if not _tut_active:
		return
	if event is InputEventScreenTouch:
		if event.pressed:
			_tut_touch_start = event.position
			_tut_touching = true
		elif _tut_touching:
			_tut_touching = false
			var d: Vector2 = event.position - _tut_touch_start
			if absf(d.x) < 60.0 and absf(d.y) < 60.0:
				return
			if absf(d.x) > absf(d.y):
				_tut_gesture("right" if d.x > 0.0 else "left")
			else:
				_tut_gesture("down" if d.y > 0.0 else "up")


func _tut_poll_keys() -> void:
	if Input.is_action_just_pressed("move_left"):
		_tut_gesture("left")
	elif Input.is_action_just_pressed("move_right"):
		_tut_gesture("right")
	elif Input.is_action_just_pressed("jump"):
		_tut_gesture("up")
	elif Input.is_action_just_pressed("slide"):
		_tut_gesture("down")


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
	var tip := _label("Your realm endures — only this layer's progress is lost.", 16, DIM, HORIZONTAL_ALIGNMENT_CENTER)
	tip.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	v.add_child(tip)
	_show_overlay(_death_root, false)


# ================================================================ overlay helper
func _show_overlay(root: Control, v: bool) -> void:
	if root != null:
		root.visible = v


# ================================================================ public API
func _input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		var g = _game()
		if _tut_active:
			_tut_skip_all()
		elif _settings_root != null and _settings_root.visible:
			_show_overlay(_settings_root, false)
		elif _shop_root != null and _shop_root.visible:
			_show_overlay(_shop_root, false)
		elif _journal_root != null and _journal_root.visible:
			_show_overlay(_journal_root, false)
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
	if _daily_btn != null:
		var g = _game()
		if g != null and g.has_method("daily_available") and g.daily_available():
			_daily_btn.text = "✦  Claim Daily Qi"
			_daily_btn.disabled = false
		else:
			var st: int = g.get_daily_streak() if (g != null and g.has_method("get_daily_streak")) else 0
			_daily_btn.text = ("Daily Qi claimed  ·  Day %d" % st) if st > 0 else "Daily Qi claimed"
			_daily_btn.disabled = true


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
	if _tut_active:
		_tut_poll_keys()
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
