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
@export var slash_range: float = 6.0     # how far ahead a slash reaches (units)
@export var slash_cooldown: float = 0.25 # min seconds between slashes

const SLASH_LANE_TOL: float = 1.6        # x tolerance to count an enemy as "in lane"

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
var _slash_cd: float = 0.0

var _mesh: MeshInstance3D
var _box: BoxMesh
var _col: CollisionShape3D
var _shape: BoxShape3D
var _mat: StandardMaterial3D

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

	_set_height(STAND_HEIGHT, STAND_COLOR)

func _physics_process(delta: float) -> void:
	if _dead:
		velocity = Vector3.ZERO
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
	_run_time += delta
	var ramp: float = clampf(_run_time / speed_ramp_time, 0.0, 1.0)
	run_speed = lerpf(base_speed, max_speed, ramp)
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
	else:
		velocity.y = min(velocity.y, -fast_fall_speed)
		_pending_slide = true

func _end_slide() -> void:
	is_sliding = false
	_set_height(STAND_HEIGHT, STAND_COLOR)

## Resize visual + collision, keeping the feet at the body origin (y=0).
func _set_height(h: float, col: Color) -> void:
	_box.size = Vector3(BODY_WIDTH, h, BODY_WIDTH)
	_mesh.position = Vector3(0.0, h * 0.5, 0.0)
	_shape.size = Vector3(BODY_WIDTH, h, BODY_WIDTH)
	_col.position = Vector3(0.0, h * 0.5, 0.0)
	_mat.albedo_color = col

## Slash: melee forward. Destroys enemies ahead within range in the current lane.
func try_slash() -> void:
	if _dead or _slash_cd > 0.0:
		return
	_slash_cd = slash_cooldown
	_show_slash_fx()
	for e in get_tree().get_nodes_in_group("enemy"):
		if not is_instance_valid(e):
			continue
		var ahead: float = global_position.z - e.global_position.z  # >0 means enemy is in front
		var lateral: float = absf(e.global_position.x - global_position.x)
		if ahead >= -1.0 and ahead <= slash_range and lateral <= SLASH_LANE_TOL:
			e.queue_free()

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

## Called by the game coordinator when the player dies.
func on_death() -> void:
	_dead = true

## Distance readout: forward progress in whole units ("meters").
func get_distance() -> int:
	return int(max(0.0, start_z - global_position.z))
