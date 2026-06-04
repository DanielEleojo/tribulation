extends CharacterBody3D
## Player (3D lane runner): auto-runs FORWARD (-Z) at a constant speed, with gravity.
## Lanes: move_left/right (A/D, arrows, swipe) ease between 3 lanes.
## Jump:  "jump" (Space) or swipe-up; only while on the floor. Cancels a slide.
## Slide: "slide" (Down/S) or swipe-down.
##        - On the ground: crouch (shorter box) for ~0.65s, then restore.
##        - In the air: fast-fall (dive) straight down, then slide on landing.
## Placeholder visual is a colored box built in code (no art yet).

@export var base_speed: float = 12.0       # starting forward speed (units/sec, -Z)
@export var max_speed: float = 22.0        # speed cap so it stays playable
@export var speed_ramp_time: float = 90.0  # seconds of running to reach max_speed
@export var gravity: float = 30.0          # downward acceleration (units/sec^2)
@export var jump_velocity: float = 12.0  # upward velocity on jump (units/sec)
@export var fast_fall_speed: float = 30.0  # downward dive speed when sliding mid-air
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
var _game
var _snd
var _base_color: Color = STAND_COLOR  # current standing color (shifts per realm)
var _speed_mult: float = 1.0          # realm forward-speed multiplier
var _dread: bool = false
var _tendrils: CPUParticles3D
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

var _mesh: MeshInstance3D
var _box: BoxMesh
var _col: CollisionShape3D
var _shape: BoxShape3D
var _mat: StandardMaterial3D
var _dust: CPUParticles3D

func _ready() -> void:
	add_to_group("player")
	start_z = global_position.z
	_build_body()
	# Wire touch swipes to the matching actions.
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if swipe != null:
		swipe.swiped_left.connect(move_left)
		swipe.swiped_right.connect(move_right)
		swipe.swiped_up.connect(try_jump)
		swipe.swiped_down.connect(start_slide)
		swipe.tapped.connect(try_slash)

func _build_body() -> void:
	# Visual + collision boxes, offset up by half-height so the origin is at the FEET.
	_mat = StandardMaterial3D.new()
	_mat.albedo_color = STAND_COLOR

	_mesh = MeshInstance3D.new()
	_box = BoxMesh.new()
	_mesh.mesh = _box
	_mesh.material_override = _mat
	add_child(_mesh)

	_col = CollisionShape3D.new()
	_shape = BoxShape3D.new()
	_col.shape = _shape
	add_child(_col)

	_set_height(STAND_HEIGHT, _base_color)
	_build_dust()

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
	run_speed = lerpf(base_speed, max_speed, ramp) * _speed_mult + _sprint_boost
	velocity.z = -run_speed
	# Ease sideways toward the target lane's X.
	var target_x: float = float(current_lane - 1) * LANE_WIDTH
	var dx: float = target_x - global_position.x
	velocity.x = clampf(dx * LANE_SHARPNESS, -MAX_LANE_SPEED, MAX_LANE_SPEED)
	# Gravity; landing cancels downward velocity.
	velocity.y -= gravity * delta
	move_and_slide()

## Lane changes (clamped to the three lanes).
func move_left() -> void:
	if _dead:
		return
	current_lane = max(0, current_lane - 1)

func move_right() -> void:
	if _dead:
		return
	current_lane = min(LANE_COUNT - 1, current_lane + 1)

## Jump when grounded. Jumping mid-slide cancels the slide.
func try_jump() -> void:
	if _dead or not is_on_floor():
		return
	if is_sliding:
		_end_slide()
	_pending_slide = false
	velocity.y = jump_velocity
	_sfx("jump")

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

## Resize visual + collision, keeping the feet at the body origin (y=0).
func _set_height(h: float, col: Color) -> void:
	_box.size = Vector3(BODY_WIDTH, h, BODY_WIDTH)
	_mesh.position = Vector3(0.0, h * 0.5, 0.0)
	_shape.size = Vector3(BODY_WIDTH, h, BODY_WIDTH)
	_col.position = Vector3(0.0, h * 0.5, 0.0)
	_mat.albedo_color = col

## Slash: melee forward. Destroys enemies ahead within range in the current lane.
func try_slash() -> void:
	if _dead or not _running or _slash_cd > 0.0:
		return
	_slash_cd = slash_cooldown
	_sfx("slash")
	_show_slash_fx()
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

## Cultivation breakthrough: shift the demon's color (power tier set separately).
func on_breakthrough(realm_color: Color) -> void:
	_base_color = realm_color
	if not is_sliding:
		_set_height(STAND_HEIGHT, _base_color)

## Dread Form: black-red flare, writhing shadow tendrils, a speed spike.
func enter_dread_form() -> void:
	_dread = true
	# (speed/range/shield come from the Dread Form realm stats)
	_base_color = Color(0.16, 0.02, 0.06)
	_mat.emission_enabled = true
	_mat.emission = Color(0.7, 0.05, 0.10)
	if not is_sliding:
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
	_tendrils.gravity = Vector3(0.0, 1.5, 0.0)   # rise like dark smoke
	_tendrils.scale_amount_min = 0.2
	_tendrils.scale_amount_max = 0.5
	var bm := BoxMesh.new()
	bm.size = Vector3.ONE
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.08, 0.0, 0.04)
	mat.emission_enabled = true
	mat.emission = Color(0.5, 0.02, 0.08)
	bm.material = mat
	_tendrils.mesh = bm
	_tendrils.position = Vector3(0.0, 1.2, 0.3)
	add_child(_tendrils)

## Called by the game coordinator when the run starts (leaving the title screen).
func begin_run() -> void:
	_running = true

## Called by the game coordinator when the player dies.
func on_death() -> void:
	_dead = true

## Distance readout: forward progress in whole units ("meters").
## 0..1 ramp fraction of current speed (base -> max). Drives FOV / fog juice.
func get_speed_fraction() -> float:
	return clampf(_run_time / speed_ramp_time, 0.0, 1.0)

func get_distance() -> int:
	return int(max(0.0, start_z - global_position.z))
