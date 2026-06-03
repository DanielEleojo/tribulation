extends Node3D
## Obstacle spawner (3D lane runner). Places obstacles AHEAD of the player in a
## random lane, alternating two placeholder types:
##   - ground block (red)    -> jump over (or change lane)
##   - overhead bar (purple) -> slide under (or change lane)
## Spawn frequency increases over time (interval shrinks to a floor).
## Obstacles are Area3D triggers (no physics push); contact = death.
## They queue_free() once well behind the player.

const SPAWN_AHEAD: float = 70.0        # units ahead of the player (beyond view/fog)
const DESPAWN_BEHIND: float = 25.0     # units behind the player before removal
const LANE_WIDTH: float = 2.5          # must match the player's lane spacing

# Block (jump over): rests on the ground in one lane.
const BLOCK_SIZE := Vector3(2.0, 1.5, 1.5)
const BLOCK_COLOR := Color(0.85, 0.22, 0.22)

# Bar (slide under): floats at head height; standing player hits it, slider clears.
const BAR_SIZE := Vector3(2.2, 0.8, 0.8)
const BAR_BOTTOM_Y: float = 1.2
const BAR_COLOR := Color(0.45, 0.35, 0.9)

# Enemy (sect disciple): slash it, or change lane. Stands in one lane on the ground.
const ENEMY_SIZE := Vector3(0.95, 1.8, 0.95)
const ENEMY_COLOR := Color(0.75, 0.12, 0.16)

# Frequency ramp.
@export var start_interval: float = 1.4   # seconds between spawns at the start
@export var min_interval: float = 0.7      # fastest spawn rate
@export var ramp_time: float = 60.0        # seconds to reach min_interval

var player: Node3D
var game
var _elapsed: float = 0.0
var _timer: float = 0.0
var _spawn_index: int = 0

func _ready() -> void:
	randomize()
	game = get_tree().get_first_node_in_group("game")
	_timer = start_interval

func _process(delta: float) -> void:
	# Player joins its group after this node readies, so look it up lazily.
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	if game == null:
		game = get_tree().get_first_node_in_group("game")
	if game != null and game.is_dead:
		return
	_elapsed += delta
	_timer -= delta
	if _timer <= 0.0:
		_spawn()
		_timer = _current_interval()
	_cleanup()

func _current_interval() -> float:
	var t: float = clampf(_elapsed / ramp_time, 0.0, 1.0)
	return lerpf(start_interval, min_interval, t)

func _spawn() -> void:
	# Cycle the three kinds so jump / slide / slash all get exercised; random lane.
	var kind: int = _spawn_index % 3   # 0 = block, 1 = bar, 2 = enemy
	_spawn_index += 1
	var lane: int = randi() % 3
	var obs := _make_obstacle(kind)
	var x: float = float(lane - 1) * LANE_WIDTH
	obs.position = Vector3(x, 0.0, player.global_position.z - SPAWN_AHEAD)
	add_child(obs)

func _make_obstacle(kind: int) -> Area3D:
	var area := Area3D.new()
	var mesh := MeshInstance3D.new()
	var box := BoxMesh.new()
	var col := CollisionShape3D.new()
	var bshape := BoxShape3D.new()
	var mat := StandardMaterial3D.new()

	var size: Vector3
	var center_y: float
	match kind:
		1:
			size = BAR_SIZE
			center_y = BAR_BOTTOM_Y + BAR_SIZE.y * 0.5
			mat.albedo_color = BAR_COLOR
		2:
			size = ENEMY_SIZE
			center_y = ENEMY_SIZE.y * 0.5
			mat.albedo_color = ENEMY_COLOR
			mat.emission_enabled = true
			mat.emission = Color(0.4, 0.04, 0.06)
			area.add_to_group("enemy")
		_:
			size = BLOCK_SIZE
			center_y = BLOCK_SIZE.y * 0.5            # bottom sits at y=0
			mat.albedo_color = BLOCK_COLOR

	box.size = size
	mesh.mesh = box
	mesh.material_override = mat
	mesh.position = Vector3(0.0, center_y, 0.0)
	bshape.size = size
	col.shape = bshape
	col.position = Vector3(0.0, center_y, 0.0)

	area.add_child(mesh)
	area.add_child(col)
	area.body_entered.connect(_on_obstacle_body_entered)
	return area

func _on_obstacle_body_entered(body: Node) -> void:
	if body.is_in_group("player") and game != null:
		game.die()

func _cleanup() -> void:
	var kill_z: float = player.global_position.z + DESPAWN_BEHIND
	for child in get_children():
		if child.position.z > kill_z:
			child.queue_free()
