extends CharacterBody3D
## Player (3D lane runner): auto-runs FORWARD (-Z) at a constant speed, with gravity.
## Lanes: move_left/right (A/D, arrows, swipe) ease between 3 lanes.
## Jump:  "jump" (Space) or swipe-up; only while on the floor. Cancels a slide.
## Slide: "slide" (Down/S) or swipe-down.
##        - On the ground: crouch (shorter box) for ~0.65s, then restore.
##        - In the air: fast-fall (dive) straight down, then slide on landing.
## Placeholder visual is a colored box built in code (no art yet).

@export var base_speed: float = 12.0       # starting forward speed (units/sec, -Z)
@export var max_speed: float = 22.0        # speed the ramp reaches by speed_ramp_time
@export var speed_ramp_time: float = 90.0  # seconds of running to reach max_speed
@export var speed_creep: float = 0.07      # ENDLESS: units/sec ADDED per second past the ramp
@export var speed_creep_cap: float = 16.0  # max extra speed the endless creep can add (no plateau, but bounded)
@export var gravity: float = 48.0          # downward acceleration (units/sec^2) — snappier arc
@export var jump_velocity: float = 17.0  # upward velocity on jump (units/sec; scales with martial stage)
@export var fast_fall_speed: float = 46.0  # downward dive speed when sliding mid-air
@export var slash_range: float = 4.0     # how far ahead a slash reaches (units; grows per realm)
@export var slash_cooldown: float = 0.25 # min seconds between slashes

var slash_tol: float = 1.4               # x half-width a slash covers (grows per realm)

const STAND_HEIGHT: float = 2.0
const SLIDE_HEIGHT: float = 1.0
const BODY_WIDTH: float = 1.0
const STAND_COLOR := Color(0.95, 0.82, 0.2)   # gold standing/running
const SLIDE_COLOR := Color(0.3, 0.8, 0.9)     # cyan while sliding

const LANE_WIDTH: float = 2.5            # spacing between lanes (centers at -2.5, 0, +2.5)
const LANE_COUNT: int = 3
const LANE_SHARPNESS: float = 12.0       # how aggressively we ease toward the target lane
const MAX_LANE_SPEED: float = 18.0       # cap on sideways speed (units/sec)

const SLIDE_DURATION: float = 0.65       # seconds a ground slide lasts

var current_lane: int = 1                # 0 = left, 1 = center, 2 = right
var start_z: float = 0.0
var run_speed: float = base_speed        # current forward speed (ramps up over time)
var _run_time: float = 0.0               # elapsed alive run time, drives the ramp
var is_sliding: bool = false
var slide_time_left: float = 0.0
var _pending_slide: bool = false         # queued slide for when a fast-fall lands
var _was_on_floor: bool = false
var _dead: bool = false
var _running: bool = false            # false until the run starts (title screen)
var _slash_cd: float = 0.0
var _jump_buf: float = 0.0            # jump-input buffer (forgives early presses)
var _coyote: float = 0.0             # grace window to jump just after leaving the floor
var _air_jumps_used: int = 0         # Qi Leap (double-jump) counter, reset on landing
var _swipe                           # swipe detector (for touch-hold glide)
# Sword-flight (御剑) — a periodic aerial mode unlocked at Spirit Severing.
const FLIGHT_MIN_Y: float = 2.2
const FLIGHT_MAX_Y: float = 6.0
const FLIGHT_CLIMB: float = 7.0
const FLIGHT_DURATION: float = 8.0
const FLIGHT_COOLDOWN: float = 16.0
const FLIGHT_FIRST: float = 7.0      # delay before the first flight after reaching the realm
var _flying: bool = false
var _flight_t: float = 0.0
var _flight_cd: float = FLIGHT_FIRST
var _sword_mount: MeshInstance3D
const JUMP_BUFFER: float = 0.12
const COYOTE: float = 0.10
var _game
var _snd
var _base_color: Color = STAND_COLOR  # current standing color (shifts per realm)
var _speed_mult: float = 1.0          # realm forward-speed multiplier
var _dread: bool = false
var _tendrils: CPUParticles3D
var _aura: CPUParticles3D
var _aura_mat: StandardMaterial3D
# Iron Demon Body (shield) + Blood Sprint
var _shields: int = 0
var _max_shields: int = 0
var _invuln_t: float = 0.0            # brief immunity after absorbing a hit
var _shield_regen_t: float = 0.0
var _sprint_per_kill: float = 0.0     # Blood Sprint kick added per kill
var _sprint_boost: float = 0.0        # current decaying speed bonus
const SHIELD_REGEN_TIME: float = 9.0
const SPRINT_DECAY: float = 4.0
const SPRINT_CAP: float = 8.0

@export var model_scale_mult: float = 1.0   # fine-tune the imported model's size

var _figure: Node3D            # holds the visual (real model, or primitive fallback)
var _cape: MeshInstance3D
var _sword: MeshInstance3D
var _anim_t: float = 0.0
var _col: CollisionShape3D
var _shape: BoxShape3D
var _mat: StandardMaterial3D   # torso material — tinted per realm (primitive only)
var _dust: CPUParticles3D
var _has_model: bool = false
var _model: Node3D
var _anim_player: AnimationPlayer

func _ready() -> void:
	add_to_group("player")
	# Pull the speed curve from the Balance autoload (falls back to the @export values).
	base_speed = Balance.getf("player_base_speed", base_speed)
	max_speed = Balance.getf("player_max_speed", max_speed)
	speed_ramp_time = Balance.getf("player_speed_ramp_time", speed_ramp_time)
	speed_creep = Balance.getf("player_speed_creep", speed_creep)
	speed_creep_cap = Balance.getf("player_speed_creep_cap", speed_creep_cap)
	run_speed = base_speed
	start_z = global_position.z
	_build_body()
	# Wire touch swipes to the matching actions.
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	_swipe = swipe
	if swipe != null:
		swipe.swiped_left.connect(move_left)
		swipe.swiped_right.connect(move_right)
		swipe.swiped_up.connect(try_jump)
		swipe.swiped_down.connect(start_slide)
		swipe.tapped.connect(try_slash)

func _build_body() -> void:
	# Collision box (gameplay) — origin at the FEET; resized on slide.
	_col = CollisionShape3D.new()
	_shape = BoxShape3D.new()
	_col.shape = _shape
	add_child(_col)

	# Prefer the real rigged model; fall back to the primitive figure if missing.
	if not _build_model():
		_build_primitive_figure()
	_build_dust()

const PLAYER_GLB := "res://Models/PC animation/warrior_wuxia_animated.glb"
# Logical state -> the clip name baked into the player GLB.
const CLIP := {"run": "Running", "idle": "Idle", "jump": "Jump", "slide": "Slide", "slash": "Slash", "death": "Death"}

## Set false to use the primitive caped-swordsman figure instead of the GLB model.
const USE_PLAYER_MODEL := false

## Load the rigged player model (GLB with baked clips), loop locomotion + strip
## root motion so it runs in place. Returns false if missing (-> primitive fallback).
func _build_model() -> bool:
	if not USE_PLAYER_MODEL:
		return false   # reverted to the primitive figure
	var base: PackedScene = load(PLAYER_GLB)
	if base == null:
		return false
	_model = base.instantiate() as Node3D
	_anim_player = _find_node(_model, "AnimationPlayer") as AnimationPlayer
	if _anim_player == null:
		_model.free()
		_model = null
		return false
	for nm in _anim_player.get_animation_list():
		var a := _anim_player.get_animation(nm)
		if nm == CLIP["run"] or nm == CLIP["idle"]:
			a.loop_mode = Animation.LOOP_LINEAR
		_strip_root_motion(a)
	_figure = Node3D.new()
	add_child(_figure)
	_figure.add_child(_model)
	_model.rotation_degrees.y = 180.0   # face -Z so we see the demon's back as he flees
	_has_model = true
	call_deferred("_normalize_model")
	_play_clip("idle")
	return true

## Play a logical clip on the model (no-op if missing).
func _play_clip(logical: String) -> void:
	if _anim_player == null:
		return
	var nm: String = CLIP.get(logical, logical)
	if _anim_player.has_animation(nm):
		_anim_player.play(nm)

## Lock the horizontal drift of any root/hip position track (keep vertical bounce),
## so looping locomotion stays in place instead of snapping back each cycle.
func _strip_root_motion(anim: Animation) -> void:
	if anim == null:
		return
	for i in range(anim.get_track_count()):
		if anim.track_get_type(i) != Animation.TYPE_POSITION_3D:
			continue
		var n := anim.track_get_key_count(i)
		if n == 0:
			continue
		var minx := INF
		var maxx := -INF
		var minz := INF
		var maxz := -INF
		for k in range(n):
			var v: Vector3 = anim.track_get_key_value(i, k)
			minx = minf(minx, v.x); maxx = maxf(maxx, v.x)
			minz = minf(minz, v.z); maxz = maxf(maxz, v.z)
		# Only flatten the track that actually drifts (the root/hips) — leave limbs alone.
		if (maxx - minx) < 0.15 and (maxz - minz) < 0.15:
			continue
		var base_v: Vector3 = anim.track_get_key_value(i, 0)
		for k in range(n):
			var v: Vector3 = anim.track_get_key_value(i, k)
			anim.track_set_key_value(i, k, Vector3(base_v.x, v.y, base_v.z))

func _find_node(root: Node, cls: String) -> Node:
	var found := root.find_children("*", cls, true, false)
	return found[0] if found.size() > 0 else null

func _get_first_lib(ap: AnimationPlayer) -> AnimationLibrary:
	if ap.has_animation_library(""):
		return ap.get_animation_library("")
	var names := ap.get_animation_library_list()
	return ap.get_animation_library(names[0]) if names.size() > 0 else null

## Scale the model to STAND_HEIGHT and sit its feet at the body origin.
func _normalize_model() -> void:
	var box := _local_aabb(_model)
	var h := box.size.y
	if h <= 0.0001:
		return
	var k := (STAND_HEIGHT / h) * model_scale_mult
	_figure.scale = Vector3(k, k, k)
	_figure.position.y = -box.position.y * k   # drop feet to the body origin

## Merged local AABB of all meshes under a node.
func _local_aabb(root: Node) -> AABB:
	var box := AABB()
	for m in root.find_children("*", "MeshInstance3D", true, false):
		var a: AABB = (m as MeshInstance3D).get_aabb()
		box = a if box.size == Vector3.ZERO else box.merge(a)
	return box

## Switch the model's clip to match the player's state (let a slash finish first).
func _update_anim(_grounded: bool) -> void:
	if _anim_player == null:
		return
	if _anim_player.current_animation == CLIP["slash"] and _anim_player.is_playing():
		return
	var want: String = CLIP["run"]
	if is_sliding:
		want = CLIP["slide"]
	elif not _grounded:
		want = CLIP["jump"]
	if _anim_player.current_animation == want or not _anim_player.has_animation(want):
		return
	if want == CLIP["jump"]:
		# Time-fit the Jump clip to the airtime so it completes right as we land.
		var clip_len: float = _anim_player.get_animation(want).length
		var airtime: float = 2.0 * jump_velocity / gravity
		var spd: float = 1.0
		if airtime > 0.05 and clip_len > 0.01:
			spd = clip_len / airtime
		_anim_player.play(want, -1, spd)
	else:
		_anim_player.play(want)

## Primitive fallback: a dark caped swordsman built from boxes/capsules.
func _build_primitive_figure() -> void:
	_figure = Node3D.new()
	add_child(_figure)
	_mat = StandardMaterial3D.new()
	_mat.albedo_color = _base_color

	var torso := MeshInstance3D.new()
	var tcap := CapsuleMesh.new()
	tcap.radius = 0.34
	tcap.height = 1.25
	torso.mesh = tcap
	torso.material_override = _mat
	torso.position = Vector3(0.0, 0.95, 0.0)
	_figure.add_child(torso)

	var head := MeshInstance3D.new()
	var hs := SphereMesh.new()
	hs.radius = 0.26
	hs.height = 0.52
	head.mesh = hs
	head.material_override = _solid(Color(0.22, 0.20, 0.24))
	head.position = Vector3(0.0, 1.72, 0.0)
	_figure.add_child(head)

	var hair := MeshInstance3D.new()       # long black hair / topknot down the back
	var hb := BoxMesh.new()
	hb.size = Vector3(0.40, 0.95, 0.18)
	hair.mesh = hb
	hair.material_override = _solid(Color(0.05, 0.04, 0.06))
	hair.position = Vector3(0.0, 1.45, 0.20)
	_figure.add_child(hair)

	_cape = MeshInstance3D.new()
	var cb := BoxMesh.new()
	cb.size = Vector3(0.85, 1.5, 0.06)
	_cape.mesh = cb
	_cape.material_override = _solid(Color(0.07, 0.05, 0.09))
	_cape.position = Vector3(0.0, 1.0, 0.30)
	_cape.rotation_degrees = Vector3(8.0, 0.0, 0.0)
	_figure.add_child(_cape)

	_sword = MeshInstance3D.new()
	var sb := BoxMesh.new()
	sb.size = Vector3(0.07, 1.4, 0.07)
	_sword.mesh = sb
	var smat := _solid(Color(0.72, 0.74, 0.82))
	smat.emission_enabled = true
	smat.emission = Color(0.3, 0.4, 0.6)
	_sword.material_override = smat
	_sword.position = Vector3(0.42, 0.95, -0.12)
	_figure.add_child(_sword)

	_set_height(STAND_HEIGHT, _base_color)

func _solid(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	return m

## Continuous footstep dust kicked up behind the runner (world-space trail).
func _build_dust() -> void:
	_dust = CPUParticles3D.new()
	_dust.amount = 20
	_dust.lifetime = 0.6
	_dust.local_coords = false          # leave dust behind in the world as we move
	_dust.direction = Vector3(0.0, 1.0, 1.0)   # up and slightly backward (+Z)
	_dust.spread = 40.0
	_dust.initial_velocity_min = 1.0
	_dust.initial_velocity_max = 2.5
	_dust.gravity = Vector3(0.0, -3.0, 0.0)
	_dust.scale_amount_min = 0.1
	_dust.scale_amount_max = 0.25
	var bm := BoxMesh.new()
	bm.size = Vector3.ONE
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.5, 0.45, 0.4)
	bm.material = mat
	_dust.mesh = bm
	_dust.position = Vector3(0.0, 0.1, 0.3)
	add_child(_dust)

func _physics_process(delta: float) -> void:
	if _dead:
		velocity = Vector3.ZERO
		move_and_slide()
		return

	# Before the run starts (title screen), idle in place (just settle on the floor).
	if not _running:
		velocity.x = 0.0
		velocity.z = 0.0
		velocity.y -= gravity * delta
		move_and_slide()
		return

	# Sword-flight mode owns the whole frame while active.
	if _flying:
		_process_flight(delta)
		return

	# Lane / jump / slide keyboard input.
	if Input.is_action_just_pressed("move_left"):
		move_left()
	if Input.is_action_just_pressed("move_right"):
		move_right()
	if Input.is_action_just_pressed("jump"):
		try_jump()
	if Input.is_action_just_pressed("slide"):
		start_slide()
	if Input.is_action_just_pressed("slash"):
		try_slash()
	if _slash_cd > 0.0:
		_slash_cd -= delta

	var grounded := is_on_floor()
	if _dust != null:
		_dust.emitting = grounded   # only kick up dust while running on the ground
	_powerup_tick(delta)

	# Periodically take to the sky once Sword-flight is cultivated.
	if grounded and _can_swordfly():
		_flight_cd -= delta
		if _flight_cd <= 0.0:
			_enter_flight()
			return

	# Jump buffer + coyote time: responsive and forgiving so a leap reliably clears.
	if grounded:
		_coyote = COYOTE
		_air_jumps_used = 0
	else:
		_coyote = maxf(0.0, _coyote - delta)
	if _jump_buf > 0.0:
		_jump_buf -= delta
	if _jump_buf > 0.0:
		if _coyote > 0.0:
			# Ground jump.
			if is_sliding:
				_end_slide()
			velocity.y = jump_velocity
			_coyote = 0.0
			_jump_buf = 0.0
			_pending_slide = false
			_sfx("jump")
		elif _air_jumps_used < _max_air_jumps():
			# Qi Leap — a second jump in mid-air (Foundation+).
			velocity.y = jump_velocity * 0.92
			_air_jumps_used += 1
			_jump_buf = 0.0
			_sfx("jump")
			_spawn_burst(global_position + Vector3(0.0, 0.5, 0.0), Color(0.6, 0.85, 1.0), 12, 5.0, 0.3, 0.13)

	# A queued fast-fall slide fires the moment we touch down.
	if grounded and not _was_on_floor and _pending_slide:
		_pending_slide = false
		start_slide()
	# Slide countdown.
	if is_sliding:
		slide_time_left -= delta
		if slide_time_left <= 0.0:
			_end_slide()
	_was_on_floor = grounded

	# Forward run, ramping speed up to a cap over elapsed run time.
	# Iron Demon Body: tick invulnerability and slowly regenerate a shield.
	if _invuln_t > 0.0:
		_invuln_t -= delta
	if _shields < _max_shields:
		_shield_regen_t -= delta
		if _shield_regen_t <= 0.0:
			_shields += 1
			_shield_regen_t = SHIELD_REGEN_TIME
	# Blood Sprint decays back to baseline.
	if _sprint_boost > 0.0:
		_sprint_boost = maxf(0.0, _sprint_boost - SPRINT_DECAY * delta)

	_run_time += delta
	var ramp: float = clampf(_run_time / speed_ramp_time, 0.0, 1.0)
	run_speed = lerpf(base_speed, max_speed, ramp) * _speed_mult + _endless_creep() + _sprint_boost + _dash_bonus()
	velocity.z = -run_speed
	# Ease sideways toward the target lane's X.
	var target_x: float = float(current_lane - 1) * LANE_WIDTH
	var dx: float = target_x - global_position.x
	velocity.x = clampf(dx * LANE_SHARPNESS, -MAX_LANE_SPEED, MAX_LANE_SPEED)
	# Gravity — Cloud Tread (Nascent Soul+) slows the fall while you hold the jump.
	var g: float = gravity
	if not grounded and velocity.y < 0.0 and _can_glide() and _glide_held():
		g = gravity * 0.22
	velocity.y -= g * delta
	move_and_slide()

	if _has_model:
		_update_anim(grounded)
	else:
		_animate_figure(delta, grounded)

## Procedural run animation so the demon reads as running, not sliding: a footfall
## bounce + forward lean, cape flap and blade sway; tucks/flares in the air.
func _animate_figure(delta: float, grounded: bool) -> void:
	if _figure == null:
		return
	if is_sliding:
		_figure.position.y = 0.0
		_figure.rotation = Vector3.ZERO
		return
	_anim_t += delta * (run_speed * 0.55)
	if grounded:
		_figure.position.y = absf(sin(_anim_t)) * 0.12          # footfall bounce
		_figure.rotation.x = -0.10                               # lean into the run
		_cape.rotation_degrees.x = 8.0 + sin(_anim_t * 2.0) * 7.0
		_sword.rotation_degrees.z = sin(_anim_t) * 12.0
	else:
		_figure.position.y = 0.0
		_figure.rotation.x = -0.26                               # tuck forward mid-air
		_cape.rotation_degrees.x = 24.0                          # cape flares back

## Endless difficulty: speed keeps creeping up past the ramp (bounded), so no run
## farms forever — eventually the road outruns you. 0 until speed_ramp_time.
func _endless_creep() -> float:
	return clampf((_run_time - speed_ramp_time) * speed_creep, 0.0, speed_creep_cap)

## Lane changes (clamped to the three lanes).
func move_left() -> void:
	if _dead:
		return
	current_lane = max(0, current_lane - 1)

func move_right() -> void:
	if _dead:
		return
	current_lane = min(LANE_COUNT - 1, current_lane + 1)

## Queue a jump (buffered). Executed in _physics_process when grounded/coyote allows.
func try_jump() -> void:
	if _dead:
		return
	_jump_buf = JUMP_BUFFER

## Qi Leap: extra mid-air jumps granted from Foundation Establishment.
func _max_air_jumps() -> int:
	if _game == null:
		_game = get_tree().get_first_node_in_group("game")
	return 1 if (_game != null and _game.has_method("has_ability") and _game.has_ability("doublejump")) else 0

func _can_glide() -> bool:
	if _game == null:
		_game = get_tree().get_first_node_in_group("game")
	return _game != null and _game.has_method("has_ability") and _game.has_ability("glide")

## Glide is engaged by HOLDING jump (keyboard) or keeping a finger down (touch).
func _glide_held() -> bool:
	if Input.is_action_pressed("jump"):
		return true
	return _swipe != null and _swipe.has_method("is_holding") and _swipe.is_holding()

func is_flying() -> bool:
	return _flying

func _can_swordfly() -> bool:
	if _game == null:
		_game = get_tree().get_first_node_in_group("game")
	return _game != null and _game.has_method("has_ability") and _game.has_ability("swordflight")

func _enter_flight() -> void:
	_flying = true
	_flight_t = FLIGHT_DURATION
	if is_sliding:
		_end_slide()
	# A glowing flying-sword under the feet.
	_sword_mount = MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = Vector3(0.4, 0.12, 2.6)
	_sword_mount.mesh = bm
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.7, 0.85, 1.0)
	m.emission_enabled = true
	m.emission = Color(0.6, 0.85, 1.0)
	m.emission_energy_multiplier = 2.0
	_sword_mount.material_override = m
	_sword_mount.position = Vector3(0.0, 0.05, 0.0)
	add_child(_sword_mount)
	_sfx("jump")

func _exit_flight() -> void:
	_flying = false
	_flight_cd = FLIGHT_COOLDOWN
	if _sword_mount != null:
		_sword_mount.queue_free()
		_sword_mount = null
	# gravity resumes next frame -> a natural descent back to the road.

## Aerial control: lane + climb/dive within a band; no gravity; timed.
func _process_flight(delta: float) -> void:
	if Input.is_action_just_pressed("move_left"):
		move_left()
	if Input.is_action_just_pressed("move_right"):
		move_right()
	if Input.is_action_just_pressed("slash"):
		try_slash()
	if _slash_cd > 0.0:
		_slash_cd -= delta

	# Vertical: lift onto the sword, then climb (jump/hold) / dive (slide) within the band.
	var y := global_position.y
	var vy := 0.0
	if y < FLIGHT_MIN_Y:
		vy = FLIGHT_CLIMB
	elif Input.is_action_pressed("jump") or (_swipe != null and _swipe.is_holding()):
		vy = FLIGHT_CLIMB if y < FLIGHT_MAX_Y else 0.0
	elif Input.is_action_pressed("slide"):
		vy = -FLIGHT_CLIMB if y > FLIGHT_MIN_Y else 0.0
	velocity.y = vy

	# Forward + lane (shared with ground running).
	_run_time += delta
	var ramp: float = clampf(_run_time / speed_ramp_time, 0.0, 1.0)
	run_speed = lerpf(base_speed, max_speed, ramp) * _speed_mult + _endless_creep() + _sprint_boost + _dash_bonus()
	velocity.z = -run_speed
	var target_x: float = float(current_lane - 1) * LANE_WIDTH
	velocity.x = clampf((target_x - global_position.x) * LANE_SHARPNESS, -MAX_LANE_SPEED, MAX_LANE_SPEED)
	move_and_slide()

	if _has_model:
		_update_anim(false)
	else:
		_animate_figure(delta, false)

	_flight_t -= delta
	if _flight_t <= 0.0:
		_exit_flight()

## Slide. On the ground: crouch. In the air: fast-fall and queue a slide on landing.
func start_slide() -> void:
	if _dead:
		return
	if is_on_floor():
		if is_sliding:
			return
		is_sliding = true
		slide_time_left = SLIDE_DURATION
		_set_height(SLIDE_HEIGHT, SLIDE_COLOR)
		_sfx("slide")
	else:
		velocity.y = min(velocity.y, -fast_fall_speed)
		_pending_slide = true

func _end_slide() -> void:
	is_sliding = false
	_set_height(STAND_HEIGHT, _base_color)

## Resize collision + crouch the figure, keeping the feet at the body origin (y=0).
func _set_height(h: float, col: Color) -> void:
	_shape.size = Vector3(BODY_WIDTH, h, BODY_WIDTH)
	_col.position = Vector3(0.0, h * 0.5, 0.0)
	if _has_model:
		return   # the model crouches via its slide animation, not a Y-scale
	if _figure != null:
		_figure.scale.y = h / STAND_HEIGHT   # squash down into a slide
	if _mat != null:
		_mat.albedo_color = col

## Slash: melee forward. Destroys enemies ahead within range in the current lane.
func try_slash() -> void:
	if _dead or not _running or _slash_cd > 0.0:
		return
	# Sword-qi is not yet yours — a mortal can only endure and dodge (gated by realm).
	if _game == null:
		_game = get_tree().get_first_node_in_group("game")
	if _game != null and _game.has_method("has_ability") and not _game.has_ability("slash"):
		return
	_slash_cd = slash_cooldown
	_sfx("slash")
	_show_slash_fx()
	if _has_model:
		_play_clip("slash")
	var killed: int = 0
	for e in get_tree().get_nodes_in_group("enemy"):
		if not is_instance_valid(e):
			continue
		var ahead: float = global_position.z - e.global_position.z  # >0 means enemy is in front
		var lateral: float = absf(e.global_position.x - global_position.x)
		if ahead >= -1.0 and ahead <= slash_range and lateral <= slash_tol:
			_spawn_burst(e.global_position + Vector3(0.0, 1.0, 0.0), Color(0.85, 0.15, 0.18), 14, 5.0, 0.5, 0.18)
			e.queue_free()
			killed += 1
	if killed > 0:
		# Blood Sprint: each kill adds a decaying speed surge.
		_sprint_boost = minf(SPRINT_CAP, _sprint_boost + _sprint_per_kill * float(killed))
		if _game == null:
			_game = get_tree().get_first_node_in_group("game")
		if _game != null:
			_game.on_enemy_killed(killed)

## Brief glowing slash arc in front of the player.
func _show_slash_fx() -> void:
	var fx := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = Vector3(2.6, 2.0, 0.3)
	fx.mesh = bm
	var m := StandardMaterial3D.new()
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.albedo_color = Color(0.9, 0.95, 1.0, 0.6)
	m.emission_enabled = true
	m.emission = Color(0.8, 0.9, 1.0)
	fx.material_override = m
	fx.position = Vector3(0.0, 1.0, -2.2)
	add_child(fx)
	get_tree().create_timer(0.12).timeout.connect(fx.queue_free)
	# A quick spark burst in front to punch up the slash.
	_spawn_burst(global_position + Vector3(0.0, 1.0, -2.2), Color(0.85, 0.95, 1.0), 10, 6.0, 0.3, 0.12)

## One-shot particle burst in world space (auto-frees).
func _spawn_burst(world_pos: Vector3, color: Color, count: int, speed: float, life: float, psize: float) -> void:
	var p := CPUParticles3D.new()
	p.one_shot = true
	p.emitting = true
	p.amount = count
	p.lifetime = life
	p.explosiveness = 1.0
	p.spread = 55.0
	p.direction = Vector3(0.0, 1.0, 0.0)
	p.initial_velocity_min = speed * 0.4
	p.initial_velocity_max = speed
	p.gravity = Vector3(0.0, -12.0, 0.0)
	p.scale_amount_min = psize * 0.6
	p.scale_amount_max = psize
	var bm := BoxMesh.new()
	bm.size = Vector3.ONE
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.emission_enabled = true
	mat.emission = color
	bm.material = mat
	p.mesh = bm
	var host := get_parent()
	if host == null:
		host = self
	host.add_child(p)
	p.global_position = world_pos
	get_tree().create_timer(life + 0.3).timeout.connect(p.queue_free)

## Play a named SFX via the sound manager (no-op if missing).
func _sfx(n: String) -> void:
	if _snd == null:
		_snd = get_tree().get_first_node_in_group("sound")
	if _snd != null:
		_snd.play(n)

## Apply a realm's power tier (called at start for realm 0, and on each breakthrough).
func apply_realm_stats(d: Dictionary) -> void:
	slash_range = float(d.get("range", slash_range))
	slash_tol = float(d.get("tol", slash_tol))
	_speed_mult = float(d.get("speed", _speed_mult))
	_sprint_per_kill = float(d.get("sprint", _sprint_per_kill))
	var new_max: int = int(d.get("shield", _max_shields))
	if new_max > _max_shields:
		_shields += (new_max - _max_shields)   # grant the new shield slot(s) filled
	_max_shields = new_max
	_shields = mini(_shields, _max_shields)

## Iron Demon Body: absorb one hit if shielded or briefly invulnerable.
func try_absorb_hit() -> bool:
	if _invuln_t > 0.0:
		return true
	if _shields > 0:
		_shields -= 1
		_invuln_t = 1.0
		_shield_regen_t = SHIELD_REGEN_TIME
		return true
	return false

func get_shields() -> int:
	return _shields

## Iron Aegis talisman grants an extra absorb charge.
func grant_shield() -> void:
	_shields += 1
	_max_shields = maxi(_max_shields, _shields)

func _dash_bonus() -> float:
	if _game != null and _game.has_method("is_powerup_active") and _game.is_powerup_active("dash"):
		return 12.0
	return 0.0

## Per-frame pill effects: Soul-Attraction pulls orbs; Sword-Qi Dash keeps invuln.
func _powerup_tick(delta: float) -> void:
	if _game == null:
		_game = get_tree().get_first_node_in_group("game")
	if _game == null or not _game.has_method("is_powerup_active"):
		return
	if _game.is_powerup_active("magnet"):
		for orb in get_tree().get_nodes_in_group("orb"):
			if is_instance_valid(orb) and orb.global_position.distance_to(global_position) < 12.0:
				orb.global_position = orb.global_position.move_toward(global_position + Vector3(0.0, 1.0, 0.0), 18.0 * delta)
	if _game.is_powerup_active("dash"):
		_invuln_t = maxf(_invuln_t, 0.15)   # plow on, untouchable while dashing

## Cultivation breakthrough: shift the demon's color (power tier set separately).
func on_breakthrough(realm_color: Color) -> void:
	_base_color = realm_color
	if _has_model:
		return   # realm tint of the textured model handled later (modulate/shader)
	if not is_sliding:
		_set_height(STAND_HEIGHT, _base_color)

## Dread Form: black-red flare, writhing shadow tendrils, a speed spike.
func enter_dread_form() -> void:
	_dread = true
	# Ascension: a radiant gold aura (speed/range/shield come from the realm stats).
	_base_color = Color(0.95, 0.90, 0.70)
	if _mat != null:
		_mat.emission_enabled = true
		_mat.emission = Color(1.0, 0.9, 0.5)
	if not is_sliding and not _has_model:
		_set_height(STAND_HEIGHT, _base_color)
	_build_tendrils()

func _build_tendrils() -> void:
	_tendrils = CPUParticles3D.new()
	_tendrils.amount = 36
	_tendrils.lifetime = 0.7
	_tendrils.local_coords = false
	_tendrils.direction = Vector3(0.0, 0.6, 1.0)
	_tendrils.spread = 50.0
	_tendrils.initial_velocity_min = 2.0
	_tendrils.initial_velocity_max = 5.0
	_tendrils.gravity = Vector3(0.0, 1.5, 0.0)   # rise like ascending qi
	_tendrils.scale_amount_min = 0.2
	_tendrils.scale_amount_max = 0.5
	var bm := BoxMesh.new()
	bm.size = Vector3.ONE
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.9, 0.8, 0.4)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.9, 0.5)
	bm.material = mat
	_tendrils.mesh = bm
	_tendrils.position = Vector3(0.0, 1.2, 0.3)
	add_child(_tendrils)

## Cultivation aura — soft rising motes whose color/strength grow per realm.
func set_aura(c: Color, intensity: float) -> void:
	if _aura == null:
		_aura = CPUParticles3D.new()
		_aura.amount = 20
		_aura.lifetime = 1.1
		_aura.local_coords = false
		_aura.direction = Vector3(0.0, 1.0, 0.0)
		_aura.spread = 22.0
		_aura.initial_velocity_min = 0.5
		_aura.initial_velocity_max = 1.3
		_aura.gravity = Vector3(0.0, 0.8, 0.0)
		_aura.scale_amount_min = 0.07
		_aura.scale_amount_max = 0.17
		var bm := BoxMesh.new()
		bm.size = Vector3.ONE
		_aura_mat = StandardMaterial3D.new()
		_aura_mat.emission_enabled = true
		bm.material = _aura_mat
		_aura.mesh = bm
		_aura.position = Vector3(0.0, 1.0, 0.0)
		add_child(_aura)
	_aura_mat.albedo_color = c
	_aura_mat.emission = c
	_aura_mat.emission_energy_multiplier = 0.6 + intensity * 2.0
	_aura.emitting = intensity > 0.01   # a mortal radiates nothing

## Jump power scales with martial stage (set by the game coordinator).
func set_jump_power(v: float) -> void:
	jump_velocity = v

## Called by the game coordinator when the run starts (leaving the title screen).
func begin_run(difficulty_offset: float = 0.0) -> void:
	_running = true
	_run_time = difficulty_offset   # higher realms start deeper into the difficulty curve

## Called by the game coordinator when the player dies.
func on_death() -> void:
	_dead = true
	if _has_model:
		_play_clip("death")

## Distance readout: forward progress in whole units ("meters").
## 0..1 ramp fraction of current speed (base -> max). Drives FOV / fog juice.
func get_speed_fraction() -> float:
	return clampf(_run_time / speed_ramp_time, 0.0, 1.0)

func get_distance() -> int:
	return int(max(0.0, start_z - global_position.z))
