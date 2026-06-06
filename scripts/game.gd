
extends Node3D
## Root coordinator: owns dead/alive state, wires death + restart, and sets up
## the 3D world (lighting + environment) in code so we don't hand-author resources.
## Wiring happens here because the root readies LAST (every child already exists).

signal died
signal qi_changed(qi: float, qi_max: float)
signal net_changed(net: float)
signal souls_changed(souls: int)

@export var qi_max: float = 100.0      # Qi needed to trigger a Qi Burst
@export var qi_per_kill: float = 20.0  # Qi gained per enemy slain (5 kills = burst)

@export var net_close_rate: float = 0.025   # how fast the Heavenly Net closes (per sec)
@export var net_push_per_kill: float = 0.12 # how much a kill pushes the net back
@export var net_burst_relief: float = 0.30  # extra net relief from a Qi Burst

var started: bool = false             # false on the title screen, true once running
var is_dead: bool = false
var qi: float = 0.0
var net: float = 0.0                  # 0 = open, 1 = closed (death)
var souls: int = 0                    # Demon Souls collected this run (+1 per kill)
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
	{"name": "Mortal Husk",     "souls": 0,  "color": Color(0.85, 0.78, 0.55), "range": 4.0, "tol": 1.4, "shield": 0, "speed": 1.00, "sprint": 0.0},
	{"name": "Blood Awakening", "souls": 4,  "color": Color(0.90, 0.45, 0.30), "range": 4.6, "tol": 1.4, "shield": 0, "speed": 1.00, "sprint": 1.5},
	{"name": "Sinister Core",   "souls": 10, "color": Color(0.80, 0.28, 0.34), "range": 5.4, "tol": 2.6, "shield": 0, "speed": 1.00, "sprint": 1.5},
	{"name": "Demon Flesh",     "souls": 18, "color": Color(0.70, 0.24, 0.40), "range": 6.0, "tol": 2.6, "shield": 1, "speed": 1.00, "sprint": 2.0},
	{"name": "Shadow Soul",     "souls": 28, "color": Color(0.50, 0.20, 0.52), "range": 6.6, "tol": 2.8, "shield": 1, "speed": 1.05, "sprint": 2.0},
	{"name": "Dread Form",      "souls": 40, "color": Color(0.20, 0.04, 0.10), "range": 8.5, "tol": 4.0, "shield": 2, "speed": 1.25, "sprint": 3.0},
]
var realm: int = 0

## The pursuing martial artists also cultivate: their rank rises over the run, and
## their techniques (the hazards you dodge) grow visually from a dull flicker to a
## blazing heaven-cleaving wave. accent = qi color, energy = glow, scale = size.
var _tiers: Array = [
	{"name": "Third-rate",   "accent": Color(0.75, 0.75, 0.80), "energy": 0.7, "scale": 0.90},
	{"name": "Second-rate",  "accent": Color(0.50, 0.70, 1.00), "energy": 1.1, "scale": 1.00},
	{"name": "First-rate",   "accent": Color(0.40, 1.00, 0.60), "energy": 1.5, "scale": 1.12},
	{"name": "Peak",         "accent": Color(0.85, 0.50, 1.00), "energy": 2.0, "scale": 1.25},
	{"name": "Transcendent", "accent": Color(1.00, 0.90, 0.55), "energy": 2.7, "scale": 1.45},
]
# "li fled" thresholds per stage. Intervals grow at HALF a doubling per stage
# (sqrt(2)^n) from a mildly-raised base of 180 li third->second:
# 180, ~254, ~360, ~509 -> cumulative below.
const TIER_DIST: Array = [0, 180, 434, 794, 1303]
var enemy_tier: int = 0

# Jump power scales with martial stage: base at third-rate, capped max by first-rate.
const JUMP_BASE: float = 17.0
const JUMP_MAX: float = 20.0

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
	if swipe != null:
		swipe.tapped.connect(_on_tap)
	var net_overlay := get_tree().get_first_node_in_group("net_overlay")
	if net_overlay != null:
		net_changed.connect(net_overlay.on_net_changed)
	_cam = get_tree().get_first_node_in_group("camera")
	_sound = get_tree().get_first_node_in_group("sound")
	_setup_atmosphere()
	_apply_theme(0)   # start in the forest

	qi_changed.emit(qi, qi_max)   # initialize the HUD bar at 0
	net_changed.emit(net)
	souls_changed.emit(souls)
	if _hud != null:
		_hud.set_realm(String(_realms[0]["name"]))
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

	# The Heavenly Net steadily closes; full closure is death.
	net = minf(1.0, net + net_close_rate * delta)
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
		if _hud != null:
			_hud.flash(Color(0.55, 0.6, 0.95))   # iron-body absorb flash
		return
	die()

## Called by an obstacle when it touches the player.
func die() -> void:
	if is_dead:
		return
	is_dead = true
	_sfx("death")
	_shake(0.9)
	_hitstop(0.12)
	died.emit()

## Called by the player after a slash kills enemies. Charges Qi; bursts at max.
func on_enemy_killed(count: int = 1) -> void:
	if is_dead:
		return
	souls += count
	souls_changed.emit(souls)
	_check_breakthrough()
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
	if _hud != null:
		_hud.set_realm(rname)
		_hud.show_banner(rname)
		_hud.flash(Color(1.0, 0.85, 0.4))
	_sfx("breakthrough")
	_shake(0.4)
	_apply_theme(idx)
	if _player != null:
		_player.apply_realm_stats(data)
		_player.on_breakthrough(rcolor)
	if rname == "Dread Form":
		_enter_dread_form()

## Announce a stronger rank of pursuer closing in.
func _on_tier_up() -> void:
	var tname: String = String(_tiers[enemy_tier]["name"])
	if _hud != null:
		_hud.show_banner(tname + " Martial Artists")
		_hud.flash(Color(0.55, 0.65, 1.0))
	_sfx("breakthrough")
	_shake(0.3)

## Environment theme by realm: forest flight (early) -> sect grounds at night (mid).
## Dread Form's hellscape is applied separately in _enter_dread_form.
func _apply_theme(r: int) -> void:
	var ground = get_node_or_null("Ground")
	if r <= 1:
		# Forest — fleeing through the woods.
		if _sky_mat != null:
			_sky_mat.panorama = _tex_forest
		if _env != null:
			_env.background_energy_multiplier = 1.0
			_env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
			_env.ambient_light_energy = 0.8
			_env.fog_light_color = Color(0.34, 0.40, 0.32)
			_env.fog_density = FOG_BASE
		if ground != null:
			ground.set_theme(Color(0.22, 0.20, 0.14), Color(0.18, 0.17, 0.11), Color(0.45, 0.50, 0.36))
	else:
		# Sect grounds at night — now you are the hunter on their turf.
		if _sky_mat != null:
			_sky_mat.panorama = _tex_night
		if _env != null:
			_env.background_energy_multiplier = 1.0
			_env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
			_env.ambient_light_energy = 0.55
			_env.fog_light_color = Color(0.10, 0.08, 0.12)
			_env.fog_density = FOG_BASE
		if ground != null:
			ground.set_theme(Color(0.18, 0.18, 0.23), Color(0.13, 0.13, 0.18), Color(0.60, 0.50, 0.30))

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
	_shake(0.9)
	_hitstop(0.15)
	# Permanent blood color grade for the rest of the run: dim the sky into a
	# hellscape and flood the scene with crimson ambient + fog.
	if _env != null:
		_env.background_energy_multiplier = 0.4
		_env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		_env.ambient_light_color = Color(0.45, 0.16, 0.20)
		_env.ambient_light_energy = 0.9
		_env.fog_light_color = Color(0.30, 0.04, 0.06)
	var ground = get_node_or_null("Ground")
	if ground != null:
		ground.set_theme(Color(0.20, 0.06, 0.06), Color(0.13, 0.04, 0.04), Color(0.60, 0.12, 0.10))
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
