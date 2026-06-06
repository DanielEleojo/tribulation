
extends Node3D
## Root coordinator: owns dead/alive state, wires death + restart, and sets up
## the 3D world (lighting + environment) in code so we don't hand-author resources.
## Wiring happens here because the root readies LAST (every child already exists).

signal died
signal qi_changed(qi: float, qi_max: float)
signal net_changed(net: float)
signal souls_changed(souls: int)
signal combo_changed(combo: int, mult: float)

@export var qi_max: float = 100.0      # Qi needed to trigger a Qi Burst
@export var qi_per_kill: float = 20.0  # Qi gained per enemy slain (5 kills = burst)

@export var net_close_rate: float = 0.025   # how fast the Heavenly Net closes (per sec)
@export var net_push_per_kill: float = 0.12 # how much a kill pushes the net back
@export var net_burst_relief: float = 0.30  # extra net relief from a Qi Burst

var started: bool = false             # false on the title screen, true once running
var is_dead: bool = false
var qi: float = 0.0
var net: float = 0.0                  # 0 = open, 1 = closed (death)
var souls: int = 0                    # Demon Souls collected this run
var combo: int = 0                    # streak of kills/orbs (resets when hit); scales soul gain
var _best: int = 0                    # best distance ("li") ever, persisted
var _hud
var _cam
var _player
var _env: Environment
var _sound
var _sky_mat: PanoramaSkyMaterial
var _tex_forest
var _tex_night

const FOG_BASE: float = 0.012
const FOG_MAX: float = 0.020

# Cultivation realms (v1: 1-6, climaxing at Dread Form). Advanced by Demon Souls
# collected this run (placeholder for the persistent realm system in Phase 4).
## Each realm escalates power: range/tol = slash reach & lane width, shield =
## Iron Demon Body hits absorbed, speed = forward-speed multiplier, sprint =
## Blood Sprint speed kick per kill. Mortal Husk is deliberately weak/fragile.
var _realms: Array = [
	{"name": "Qi Condensation",       "souls": 0,   "color": Color(0.62, 0.66, 0.72), "range": 4.0, "tol": 1.4, "shield": 0, "speed": 1.00, "sprint": 0.0},
	{"name": "Foundation Establishment", "souls": 8, "color": Color(0.55, 0.82, 0.62), "range": 4.6, "tol": 1.4, "shield": 0, "speed": 1.00, "sprint": 1.5},
	{"name": "Golden Core",           "souls": 20,  "color": Color(0.95, 0.80, 0.35), "range": 5.4, "tol": 2.6, "shield": 0, "speed": 1.00, "sprint": 1.5},
	{"name": "Nascent Soul",          "souls": 42,  "color": Color(0.45, 0.70, 1.00), "range": 6.0, "tol": 2.6, "shield": 1, "speed": 1.00, "sprint": 2.0},
	{"name": "Spirit Severing",       "souls": 75,  "color": Color(0.72, 0.48, 1.00), "range": 6.6, "tol": 2.8, "shield": 1, "speed": 1.05, "sprint": 2.0},
	{"name": "Ascension",             "souls": 120, "color": Color(1.00, 0.95, 0.70), "range": 8.5, "tol": 4.0, "shield": 2, "speed": 1.25, "sprint": 3.0},
]
var realm: int = 0

## The road is long and hard: a mortal can only ENDURE and dodge. Verbs are earned
## by ascending — which realm first grants each ability (cumulative). R3 implements
## the new verbs (dash/glide/sword-flight/tribulation) behind these same gates.
const ABILITY_REALM := {
	"run": 0, "jump": 0, "slide": 0, "lane": 0,   # Qi Condensation: mortal, dodge-only
	"doublejump": 1,                                # Foundation Establishment (Qi Leap)
	"slash": 2, "qi": 2,                            # Golden Core: fight back; Qi cultivation
	"glide": 3,                                     # Nascent Soul
	"swordflight": 4,                               # Spirit Severing
	"tribulation": 5,                              # Ascension
}
## Newly-awakened art announced at each breakthrough.
const UNLOCKS := {
	1: "Qi Leap (double jump)", 2: "Sword-qi & Qi cultivation", 3: "Cloud Tread (glide)",
	4: "Sword-flight 御剑", 5: "Heavenly Tribulation",
}

## True if the current cultivation realm grants this ability.
func has_ability(name: String) -> bool:
	return realm >= int(ABILITY_REALM.get(name, 0))

## Per-cultivation-stage WORLD: sky, ambient/fog, ground colors, hazard palette
## (low=jump-hazard hue, high=slide-hazard hue, foe=enemy robe), foe identity, aura.
## The world, the threats, and the cultivator's aura all evolve as you ascend.
var _stages: Array = [
	{"forest": true,  "amb": Color(0.55,0.60,0.50), "amb_e": 0.85, "fog": Color(0.34,0.40,0.32), "dens": 0.012, "bg": 1.0,
	 "g": [Color(0.22,0.20,0.14), Color(0.18,0.17,0.11), Color(0.14,0.13,0.10), Color(0.50,0.62,0.32)],
	 "aura": Color(0.60,0.66,0.72), "low": Color(0.55,0.42,0.25), "high": Color(0.50,0.70,0.45), "foe": Color(0.45,0.32,0.22), "foename": "wild beasts"},
	{"forest": true,  "amb": Color(0.50,0.56,0.55), "amb_e": 0.80, "fog": Color(0.30,0.36,0.36), "dens": 0.013, "bg": 1.0,
	 "g": [Color(0.20,0.20,0.18), Color(0.16,0.16,0.15), Color(0.12,0.12,0.12), Color(0.50,0.80,0.60)],
	 "aura": Color(0.55,0.82,0.62), "low": Color(0.50,0.45,0.30), "high": Color(0.45,0.75,0.60), "foe": Color(0.50,0.45,0.35), "foename": "rogue cultivators"},
	{"forest": false, "amb": Color(0.50,0.50,0.60), "amb_e": 0.55, "fog": Color(0.10,0.09,0.13), "dens": 0.012, "bg": 1.0,
	 "g": [Color(0.18,0.18,0.23), Color(0.13,0.13,0.18), Color(0.10,0.10,0.14), Color(0.95,0.80,0.35)],
	 "aura": Color(0.95,0.80,0.35), "low": Color(0.95,0.55,0.20), "high": Color(0.30,0.80,1.00), "foe": Color(0.80,0.82,0.92), "foename": "sect disciples"},
	{"forest": false, "amb": Color(0.45,0.55,0.70), "amb_e": 0.60, "fog": Color(0.12,0.16,0.26), "dens": 0.011, "bg": 1.1,
	 "g": [Color(0.20,0.24,0.32), Color(0.15,0.18,0.26), Color(0.12,0.14,0.20), Color(0.45,0.70,1.00)],
	 "aura": Color(0.45,0.70,1.00), "low": Color(0.40,0.60,1.00), "high": Color(0.50,0.85,1.00), "foe": Color(0.55,0.70,0.95), "foename": "spirit beasts"},
	{"forest": false, "amb": Color(0.55,0.45,0.70), "amb_e": 0.60, "fog": Color(0.18,0.12,0.26), "dens": 0.012, "bg": 1.1,
	 "g": [Color(0.24,0.18,0.30), Color(0.18,0.13,0.24), Color(0.14,0.10,0.18), Color(0.72,0.48,1.00)],
	 "aura": Color(0.72,0.48,1.00), "low": Color(0.80,0.40,1.00), "high": Color(0.70,0.50,1.00), "foe": Color(0.50,0.35,0.55), "foename": "demonic cultivators"},
	{"forest": false, "amb": Color(0.85,0.80,0.62), "amb_e": 1.10, "fog": Color(0.70,0.62,0.40), "dens": 0.012, "bg": 1.4,
	 "g": [Color(0.30,0.28,0.22), Color(0.24,0.22,0.17), Color(0.18,0.16,0.12), Color(1.00,0.92,0.55)],
	 "aura": Color(1.00,0.95,0.70), "low": Color(1.00,0.85,0.40), "high": Color(0.90,0.95,1.00), "foe": Color(0.90,0.85,0.70), "foename": "inner demons"},
]

## Hazard palette (jump/slide hues + foe robe) for the current cultivation stage.
func hazard_style() -> Dictionary:
	var s: Dictionary = _stages[clampi(realm, 0, _stages.size() - 1)]
	return {"low": s["low"], "high": s["high"], "foe": s["foe"]}

## Identity of the foes on this stretch of road (wild beasts -> ... -> inner demons).
func foe_name() -> String:
	return String(_stages[clampi(realm, 0, _stages.size() - 1)]["foename"])

## Wuxia minor layer (1..10) WITHIN the current major realm, derived from how far
## souls have progressed toward the next realm's threshold.
func minor_level() -> int:
	var lo: int = int(_realms[realm]["souls"])
	var hi: int = int(_realms[realm + 1]["souls"]) if realm + 1 < _realms.size() else lo + 60
	var span: int = maxi(1, hi - lo)
	var f: float = clampf(float(souls - lo) / float(span), 0.0, 0.999)
	return int(f * 10.0) + 1

func _layer_text(n: int) -> String:
	if n >= 10:
		return "Great Perfection"
	var suffix := "th"
	if n == 1: suffix = "st"
	elif n == 2: suffix = "nd"
	elif n == 3: suffix = "rd"
	return "%d%s Layer" % [n, suffix]

## "Realm · Nth Layer" on the HUD; called on start, souls change, and breakthrough.
func _refresh_realm() -> void:
	if _hud != null:
		_hud.set_realm("%s · %s" % [String(_realms[realm]["name"]), _layer_text(minor_level())])

## The pursuing martial artists also cultivate: their rank rises over the run, and
## their techniques (the hazards you dodge) grow visually from a dull flicker to a
## blazing heaven-cleaving wave. accent = qi color, energy = glow, scale = size.
var _tiers: Array = [
	{"name": "Mortal",          "accent": Color(0.75, 0.75, 0.80), "energy": 0.7, "scale": 0.90},
	{"name": "Qi Condensation", "accent": Color(0.50, 0.70, 1.00), "energy": 1.1, "scale": 1.00},
	{"name": "Foundation",      "accent": Color(0.40, 1.00, 0.60), "energy": 1.5, "scale": 1.12},
	{"name": "Golden Core",     "accent": Color(0.95, 0.80, 0.35), "energy": 2.0, "scale": 1.25},
	{"name": "Nascent Soul",    "accent": Color(1.00, 0.92, 0.60), "energy": 2.7, "scale": 1.45},
]
# "li fled" thresholds per stage. Intervals grow at HALF a doubling per stage
# (sqrt(2)^n) from a mildly-raised base of 180 li third->second:
# 180, ~254, ~360, ~509 -> cumulative below.
const TIER_DIST: Array = [0, 180, 434, 794, 1303]
var enemy_tier: int = 0

# Jump power scales with martial stage: base at third-rate, capped max by first-rate.
const JUMP_BASE: float = 14.0
const JUMP_MAX: float = 16.0

## Current foe-rank style, read by the spawner when building a technique/hazard.
func tier_style() -> Dictionary:
	return _tiers[enemy_tier]

## Jump velocity for the current martial stage (12 at third-rate -> 14 by first-rate+).
func _jump_for_tier(t: int) -> float:
	return lerpf(JUMP_BASE, JUMP_MAX, clampf(float(t) / 2.0, 0.0, 1.0))

func _ready() -> void:
	add_to_group("game")
	_setup_world()

	var player := get_tree().get_first_node_in_group("player")
	_player = player
	var hud := get_tree().get_first_node_in_group("hud")
	_hud = hud
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if player != null:
		died.connect(player.on_death)
	if hud != null:
		died.connect(hud.on_death)
		qi_changed.connect(hud.on_qi_changed)
		souls_changed.connect(hud.on_souls_changed)
		combo_changed.connect(hud.on_combo_changed)
	if swipe != null:
		swipe.tapped.connect(_on_tap)
	var net_overlay := get_tree().get_first_node_in_group("net_overlay")
	if net_overlay != null:
		net_changed.connect(net_overlay.on_net_changed)
	_cam = get_tree().get_first_node_in_group("camera")
	_sound = get_tree().get_first_node_in_group("sound")
	_setup_atmosphere()
	_apply_theme(0)   # start in the forest

	_load_best()
	qi_changed.emit(qi, qi_max)   # initialize the HUD bar at 0
	net_changed.emit(net)
	souls_changed.emit(souls)
	combo_changed.emit(combo, 1.0)
	if _hud != null:
		_refresh_realm()
		_hud.set_best(_best)
		_hud.set_qi_visible(has_ability("qi"))   # hidden until Golden Core
	if _player != null:
		_player.apply_realm_stats(_realms[0])   # weak Mortal Husk baseline
		_player.set_jump_power(_jump_for_tier(enemy_tier))   # third-rate base jump

func _setup_world() -> void:
	# Environment: real CC0 night-sky HDRI (Poly Haven) + image-based lighting + fog.
	var env := Environment.new()
	_tex_forest = load("res://assets/backgrounds/sky_forest.hdr")
	_tex_night = load("res://assets/backgrounds/sky_night.hdr")
	_sky_mat = PanoramaSkyMaterial.new()
	_sky_mat.panorama = _tex_forest   # start the run fleeing through the forest
	var sky := Sky.new()
	sky.sky_material = _sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.8
	env.fog_enabled = true
	env.fog_light_color = Color(0.10, 0.07, 0.10)
	env.fog_density = FOG_BASE
	_env = env
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	# Dim, cool moonlight.
	var moonlight := DirectionalLight3D.new()
	moonlight.rotation_degrees = Vector3(-45, -35, 0)
	moonlight.light_color = Color(0.62, 0.70, 0.95)
	moonlight.light_energy = 0.55
	moonlight.shadow_enabled = true
	add_child(moonlight)

func _process(delta: float) -> void:
	# Title screen: wait for any input to start the run.
	if not started:
		if _start_pressed():
			start_game()
		return
	# Restart (Enter) only acts on the death screen.
	if is_dead:
		if Input.is_action_just_pressed("restart"):
			restart()
		return
	# Thicken the fog as the run speeds up (sense of speed/pressure).
	if _env != null and _player != null and _player.has_method("get_speed_fraction"):
		_env.fog_density = lerpf(FOG_BASE, FOG_MAX, _player.get_speed_fraction())
	# Mirror Iron Demon Body charges to the HUD.
	if _hud != null and _player != null:
		_hud.set_shields(_player.get_shields())

	# The hunters cultivate: raise their rank as we flee deeper.
	if _player != null:
		var d: int = _player.get_distance()
		var t: int = 0
		for i in range(TIER_DIST.size()):
			if d >= int(TIER_DIST[i]):
				t = i
		if t != enemy_tier:
			enemy_tier = t
			if _player != null:
				_player.set_jump_power(_jump_for_tier(enemy_tier))
			_on_tier_up()

	# The Tribulation steadily gathers; full closure is death. But a cultivator who has
	# reached Ascension defies heaven — the net can no longer fully close on them
	# (Heaven Defiance); only the Heavenly Tribulation lightning can fell them now.
	var cap: float = 0.85 if has_ability("tribulation") else 1.0
	net = minf(cap, net + net_close_rate * delta)
	net_changed.emit(net)
	if net >= 1.0:
		die()

## Hazard/enemy contact. Iron Demon Body absorbs the hit if available; else death.
func player_hit() -> void:
	if is_dead or not started:
		return
	if _player != null and _player.try_absorb_hit():
		_sfx("breakthrough")
		_shake(0.45)
		_reset_combo()                            # a blow breaks the streak (you survived)
		if _hud != null:
			_hud.flash(Color(0.55, 0.6, 0.95))   # iron-body absorb flash
		return
	die()

## Called by an obstacle when it touches the player.
func die() -> void:
	if is_dead:
		return
	is_dead = true
	var dist: int = _player.get_distance() if _player != null else 0
	if dist > _best:
		_best = dist
		_save_best()
	if _hud != null:
		_hud.set_best(_best)
	_sfx("death")
	_shake(0.9)
	_hitstop(0.12)
	died.emit()

## A Spirit Orb was run through — soul + Qi reward, builds the combo.
func on_orb_collected() -> void:
	if is_dead:
		return
	combo += 1
	var m := _combo_mult()
	souls += int(round(m))
	souls_changed.emit(souls)
	combo_changed.emit(combo, m)
	qi = minf(qi_max, qi + 4.0)
	qi_changed.emit(qi, qi_max)
	_check_breakthrough()
	_refresh_realm()
	_sfx("orb")

func _combo_mult() -> float:
	return minf(5.0, 1.0 + float(combo) * 0.1)   # +0.1x per streak, capped 5x

func _reset_combo() -> void:
	if combo != 0:
		combo = 0
		combo_changed.emit(0, 1.0)

func _load_best() -> void:
	var c := ConfigFile.new()
	if c.load("user://tribulation.cfg") == OK:
		_best = int(c.get_value("run", "best_li", 0))

func _save_best() -> void:
	var c := ConfigFile.new()
	c.load("user://tribulation.cfg")
	c.set_value("run", "best_li", _best)
	c.save("user://tribulation.cfg")

## Called by the player after a slash kills enemies. Charges Qi; bursts at max.
func on_enemy_killed(count: int = 1) -> void:
	if is_dead:
		return
	combo += count
	var m := _combo_mult()
	souls += int(round(float(count) * m))
	souls_changed.emit(souls)
	combo_changed.emit(combo, m)
	_check_breakthrough()
	_refresh_realm()
	_sfx("kill")
	_shake(0.12)
	qi = minf(qi_max, qi + qi_per_kill * float(count))
	qi_changed.emit(qi, qi_max)
	# Each kill pushes the Heavenly Net back.
	net = maxf(0.0, net - net_push_per_kill * float(count))
	net_changed.emit(net)
	if qi >= qi_max:
		_qi_burst()

## Advance cultivation realm(s) when souls cross the next threshold(s).
func _check_breakthrough() -> void:
	while realm < _realms.size() - 1 and souls >= int(_realms[realm + 1]["souls"]):
		realm += 1
		_breakthrough(realm)

func _breakthrough(idx: int) -> void:
	var data: Dictionary = _realms[idx]
	var rname: String = data["name"]
	var rcolor: Color = data["color"]
	var msg := rname
	if UNLOCKS.has(idx):
		msg += "\n" + String(UNLOCKS[idx]) + " awakened"
	if _hud != null:
		_refresh_realm()
		_hud.show_banner(msg)
		_hud.flash(Color(1.0, 0.85, 0.4))
		_hud.set_qi_visible(has_ability("qi"))   # Qi cultivation begins at Golden Core
	_sfx("breakthrough")
	_shake(0.4)
	_apply_theme(idx)
	if _player != null:
		_player.apply_realm_stats(data)
		_player.on_breakthrough(rcolor)
	if rname == "Ascension":
		_enter_dread_form()

## Announce a stronger rank of pursuer closing in.
func _on_tier_up() -> void:
	var tname: String = String(_tiers[enemy_tier]["name"])
	if _hud != null:
		_hud.show_banner(tname + " " + foe_name() + " bar the road")
		_hud.flash(Color(0.55, 0.65, 1.0))
	_sfx("breakthrough")
	_shake(0.3)

## Environment theme by realm: forest flight (early) -> sect grounds at night (mid).
## Dread Form's hellscape is applied separately in _enter_dread_form.
func _apply_theme(r: int) -> void:
	var s: Dictionary = _stages[clampi(r, 0, _stages.size() - 1)]
	if _sky_mat != null:
		_sky_mat.panorama = _tex_forest if bool(s["forest"]) else _tex_night
	if _env != null:
		_env.background_energy_multiplier = float(s["bg"])
		_env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		_env.ambient_light_color = s["amb"]
		_env.ambient_light_energy = float(s["amb_e"])
		_env.fog_light_color = s["fog"]
		_env.fog_density = float(s["dens"])
	var ground = get_node_or_null("Ground")
	if ground != null:
		var g: Array = s["g"]
		ground.set_theme(g[0], g[1], g[2], g[3])
	if _player != null and _player.has_method("set_aura"):
		_player.set_aura(s["aura"], float(r) / 5.0)   # mortal has no aura; brightens as you ascend

## Blood moon + drifting embers, parented to the camera so they sit in the sky.
func _setup_atmosphere() -> void:
	if _cam == null:
		return
	# Blood moon (unshaded emissive disc/sphere hanging ahead in the sky).
	var moon := MeshInstance3D.new()
	var sm := SphereMesh.new()
	sm.radius = 9.0
	sm.height = 18.0
	moon.mesh = sm
	var mm := StandardMaterial3D.new()
	mm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mm.albedo_color = Color(0.72, 0.12, 0.10)
	mm.emission_enabled = true
	mm.emission = Color(0.85, 0.16, 0.12)
	mm.emission_energy_multiplier = 2.2
	moon.material_override = mm
	moon.position = Vector3(16.0, 24.0, -95.0)
	_cam.add_child(moon)
	# Drifting spirit embers around the player.
	var emb := CPUParticles3D.new()
	emb.amount = 40
	emb.lifetime = 4.0
	emb.local_coords = false
	emb.direction = Vector3(0.0, 1.0, 0.0)
	emb.spread = 80.0
	emb.gravity = Vector3(0.0, 0.6, 0.0)
	emb.initial_velocity_min = 0.3
	emb.initial_velocity_max = 1.2
	emb.scale_amount_min = 0.04
	emb.scale_amount_max = 0.12
	var ebox := BoxMesh.new()
	ebox.size = Vector3.ONE
	var emat := StandardMaterial3D.new()
	emat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	emat.albedo_color = Color(0.9, 0.5, 0.2)
	emat.emission_enabled = true
	emat.emission = Color(1.0, 0.55, 0.2)
	ebox.material = emat
	emb.mesh = ebox
	# Emit in a volume around/ahead of the camera.
	emb.emission_shape = CPUParticles3D.EMISSION_SHAPE_BOX
	emb.emission_box_extents = Vector3(14.0, 6.0, 20.0)
	emb.position = Vector3(0.0, 2.0, -14.0)
	_cam.add_child(emb)

## The marquee mid-run transformation: the world ruptures, the demon flares.
func _enter_dread_form() -> void:
	# The radiant world grade is applied by the Ascension stage theme (_apply_theme).
	_shake(0.9)
	_hitstop(0.15)
	if _player != null:
		_player.enter_dread_form()

## Qi Burst: clear every enemy on the field, flash a shockwave, reset Qi.
func _qi_burst() -> void:
	for e in get_tree().get_nodes_in_group("enemy"):
		if is_instance_valid(e):
			e.queue_free()
	_spawn_burst_fx()
	_sfx("burst")
	_shake(0.5)
	_hitstop(0.06)
	qi = 0.0
	qi_changed.emit(qi, qi_max)
	# A burst also throws the Heavenly Net back.
	net = maxf(0.0, net - net_burst_relief)
	net_changed.emit(net)

func _spawn_burst_fx() -> void:
	var p := get_tree().get_first_node_in_group("player")
	if p == null:
		return
	var fx := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	fx.mesh = sphere
	var m := StandardMaterial3D.new()
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.albedo_color = Color(0.6, 0.85, 1.0, 0.5)
	m.emission_enabled = true
	m.emission = Color(0.5, 0.8, 1.0)
	fx.material_override = m
	fx.position = Vector3(0.0, 1.0, 0.0)
	p.add_child(fx)
	var tw := fx.create_tween()
	tw.set_parallel(true)
	tw.tween_property(fx, "scale", Vector3(14.0, 14.0, 14.0), 0.45)
	tw.tween_property(m, "albedo_color:a", 0.0, 0.45)
	tw.chain().tween_callback(fx.queue_free)

## Resolve a Life/Death Gate pass. Non-lethal early: a wrong gate is a penalty.
func on_gate(safe: bool) -> void:
	if is_dead:
		return
	if safe:
		qi = minf(qi_max, qi + 25.0)
		net = maxf(0.0, net - 0.15)
		_sfx("gate_good")
		_shake(0.15)
		if _hud != null:
			_hud.flash(Color(0.2, 0.9, 0.4))
	else:
		qi = maxf(0.0, qi - 40.0)
		net = minf(1.0, net + 0.30)
		_reset_combo()
		_sfx("gate_bad")
		_shake(0.5)
		if _hud != null:
			_hud.flash(Color(0.9, 0.15, 0.2))
	qi_changed.emit(qi, qi_max)
	net_changed.emit(net)
	if net >= 1.0:
		die()

## Any gameplay key starts the run from the title screen.
func _start_pressed() -> bool:
	return Input.is_action_just_pressed("jump") \
		or Input.is_action_just_pressed("slide") \
		or Input.is_action_just_pressed("slash") \
		or Input.is_action_just_pressed("move_left") \
		or Input.is_action_just_pressed("move_right") \
		or Input.is_action_just_pressed("restart")

func start_game() -> void:
	if started:
		return
	started = true
	_sfx("start")
	if _player != null:
		_player.begin_run()
	if _hud != null:
		_hud.show_title(false)

func _on_tap() -> void:
	if not started:
		start_game()
	elif is_dead:
		restart()

func restart() -> void:
	Engine.time_scale = 1.0   # safety: hitstop must never persist across a reload
	get_tree().reload_current_scene()

## Camera screen shake (no-op if camera not found).
func _shake(amount: float) -> void:
	if _cam != null:
		_cam.add_trauma(amount)

## Play a named SFX (no-op if no sound file present).
func _sfx(n: String) -> void:
	if _sound != null:
		_sound.play(n)

## Brief time freeze for impact. Uses a real-time timer so it unfreezes itself.
func _hitstop(duration: float) -> void:
	Engine.time_scale = 0.0
	await get_tree().create_timer(duration, true, false, true).timeout
	Engine.time_scale = 1.0
