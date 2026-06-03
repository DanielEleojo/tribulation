extends CharacterBody3D
## Player (3D lane runner): auto-runs FORWARD (-Z) at a constant speed, with gravity.
## Lane switching, jump, and slide are added in later steps.
## Placeholder visual is a colored box built in code (no art yet).

@export var run_speed: float = 12.0    # constant forward speed (units/sec, -Z)
@export var gravity: float = 30.0      # downward acceleration (units/sec^2)

const STAND_HEIGHT: float = 2.0
const BODY_WIDTH: float = 1.0
const STAND_COLOR := Color(0.95, 0.82, 0.2)

const LANE_WIDTH: float = 2.5          # spacing between lanes (centers at -2.5, 0, +2.5)
const LANE_COUNT: int = 3
const LANE_SHARPNESS: float = 12.0     # how aggressively we ease toward the target lane
const MAX_LANE_SPEED: float = 18.0     # cap on sideways speed (units/sec)

var current_lane: int = 1              # 0 = left, 1 = center, 2 = right
var start_z: float = 0.0
var _dead: bool = false
var _mesh: MeshInstance3D
var _box: BoxMesh
var _col: CollisionShape3D
var _shape: BoxShape3D

func _ready() -> void:
	add_to_group("player")
	start_z = global_position.z
	_build_body()
	# Wire touch swipes to lane movement.
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if swipe != null:
		swipe.swiped_left.connect(move_left)
		swipe.swiped_right.connect(move_right)

func _build_body() -> void:
	# Visual box. Offset up by half-height so the body's origin is at its FEET.
	_mesh = MeshInstance3D.new()
	_box = BoxMesh.new()
	_box.size = Vector3(BODY_WIDTH, STAND_HEIGHT, BODY_WIDTH)
	_mesh.mesh = _box
	_mesh.position = Vector3(0.0, STAND_HEIGHT * 0.5, 0.0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = STAND_COLOR
	_mesh.material_override = mat
	add_child(_mesh)

	_col = CollisionShape3D.new()
	_shape = BoxShape3D.new()
	_shape.size = Vector3(BODY_WIDTH, STAND_HEIGHT, BODY_WIDTH)
	_col.shape = _shape
	_col.position = Vector3(0.0, STAND_HEIGHT * 0.5, 0.0)
	add_child(_col)

func _physics_process(delta: float) -> void:
	if _dead:
		velocity = Vector3.ZERO
		move_and_slide()
		return

	# Keyboard lane input.
	if Input.is_action_just_pressed("move_left"):
		move_left()
	if Input.is_action_just_pressed("move_right"):
		move_right()

	# Constant forward run.
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

## Called by the game coordinator when the player dies.
func on_death() -> void:
	_dead = true

## Distance readout: forward progress in whole units ("meters").
func get_distance() -> int:
	return int(max(0.0, start_z - global_position.z))
