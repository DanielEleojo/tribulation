extends Node3D
## Obstacle spawner (3D lane runner). Places hazards AHEAD of the player.
## Width variety makes the action mechanics matter (a single-lane hazard would be
## a free lane-dodge):
##   - ground block (red)    -> JUMP. 50% full-width (forced), 50% single-lane (dodge/jump).
##   - overhead bar (purple) -> SLIDE. 50% full-width (forced), 50% single-lane (dodge/slide).
##   - enemy row (crimson)   -> SLASH. Tall (can't jump/slide over); fills 2-3 lanes,
##                              sometimes leaving one gap to dodge to.
## Hazards are Area3D triggers (no physics push); contact = death.
## They queue_free() once well behind the player.

const SPAWN_AHEAD: float = 70.0
const DESPAWN_BEHIND: float = 25.0
const LANE_WIDTH: float = 2.5          # must match the player's lane spacing
const FULL_WIDTH: float = 8.0          # spans all three lanes

const BLOCK_HEIGHT: float = 1.5
const BLOCK_DEPTH: float = 1.5
const BLOCK_LANE_WIDTH: float = 2.0
const BLOCK_COLOR := Color(0.85, 0.22, 0.22)

const BAR_HEIGHT: float = 0.8
const BAR_DEPTH: float = 0.8
const BAR_BOTTOM_Y: float = 1.2
const BAR_LANE_WIDTH: float = 2.2
const BAR_COLOR := Color(0.45, 0.35, 0.9)

const ENEMY_SIZE := Vector3(0.95, 2.6, 0.95)   # tall: can't be jumped or slid over
const ENEMY_COLOR := Color(0.75, 0.12, 0.16)

const GateScript = preload("res://scripts/gate.gd")

# Frequency ramp.
@export var start_interval: float = 1.4
@export var min_interval: float = 0.7
@export var ramp_time: float = 60.0
@export var gate_interval: float = 11.0   # seconds between Life/Death Gates

var player: Node3D
var game
var _elapsed: float = 0.0
var _timer: float = 0.0
var _gate_timer: float = 0.0
var _spawn_index: int = 0

func _ready() -> void:
	randomize()
	game = get_tree().get_first_node_in_group("game")
	_timer = start_interval
	_gate_timer = gate_interval

func _process(delta: float) -> void:
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	if game == null:
		game = get_tree().get_first_node_in_group("game")
	if game != null and (not game.started or game.is_dead):
		return
	_elapsed += delta
	_timer -= delta
	if _timer <= 0.0:
		_spawn()
		_timer = _current_interval()
	_gate_timer -= delta
	if _gate_timer <= 0.0:
		_spawn_gate()
		_gate_timer = gate_interval
	_cleanup()

func _current_interval() -> float:
	var t: float = clampf(_elapsed / ramp_time, 0.0, 1.0)
	return lerpf(start_interval, min_interval, t)

func _spawn() -> void:
	# Cycle the three kinds so jump / slide / slash all get exercised.
	var kind: int = _spawn_index % 3
	_spawn_index += 1
	var base_z: float = player.global_position.z - SPAWN_AHEAD
	match kind:
		1:
			_spawn_barrier(false, base_z)   # bar -> slide
		2:
			_spawn_enemy_row(base_z)        # enemies -> slash
		_:
			_spawn_barrier(true, base_z)    # block -> jump

func _spawn_barrier(is_block: bool, z: float) -> void:
	if randf() < 0.5:
		# Full-width: lane-switching can't save you, the action is forced.
		var obs := _make_barrier(is_block, FULL_WIDTH)
		obs.position = Vector3(0.0, 0.0, z)
		add_child(obs)
	else:
		# Single lane: dodge to another lane, or perform the action.
		var lane: int = randi() % 3
		var w: float = BLOCK_LANE_WIDTH if is_block else BAR_LANE_WIDTH
		var obs := _make_barrier(is_block, w)
		obs.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, z)
		add_child(obs)

func _spawn_gate() -> void:
	var safe_lane: int = randi() % 3
	var gate := GateScript.new()
	gate.position = Vector3(0.0, 0.0, player.global_position.z - SPAWN_AHEAD)
	add_child(gate)
	gate.setup(safe_lane, game)

func _spawn_enemy_row(z: float) -> void:
	# gap in {0,1,2} leaves that lane open; gap == 3 means a full wall (must slash).
	var gap: int = randi() % 4
	for lane in range(3):
		if lane == gap:
			continue
		var e := _make_enemy()
		e.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, z)
		add_child(e)

func _make_barrier(is_block: bool, width: float) -> Area3D:
	if is_block:
		# Heavenly Seal: a stone stele with a glowing rune facing the player.
		var size := Vector3(width, BLOCK_HEIGHT, BLOCK_DEPTH)
		var cy := BLOCK_HEIGHT * 0.5
		var area := _make_area(size, cy, false)
		_add_box(area, size, Vector3(0.0, cy, 0.0), Color(0.36, 0.35, 0.40), Color.BLACK, false)
		var rune_w := minf(width, 1.6)
		_add_box(area, Vector3(rune_w, 0.9, 0.08), Vector3(0.0, cy, -BLOCK_DEPTH * 0.5 - 0.06), Color(0.95, 0.8, 0.35), Color(1.0, 0.65, 0.2), true)
		return area
	else:
		# Formation array: a humming energy beam to slide under.
		var size := Vector3(width, BAR_HEIGHT, BAR_DEPTH)
		var cy := BAR_BOTTOM_Y + BAR_HEIGHT * 0.5
		var area := _make_area(size, cy, false)
		_add_box(area, size, Vector3(0.0, cy, 0.0), Color(0.45, 0.8, 1.0), Color(0.3, 0.7, 1.0), true)
		return area

func _make_enemy() -> Area3D:
	# Sect disciple: a robed body with a glinting blade.
	var cy := ENEMY_SIZE.y * 0.5
	var area := _make_area(ENEMY_SIZE, cy, true)
	var body := MeshInstance3D.new()
	var cap := CapsuleMesh.new()
	cap.radius = 0.42
	cap.height = ENEMY_SIZE.y
	body.mesh = cap
	var rmat := StandardMaterial3D.new()
	rmat.albedo_color = Color(0.82, 0.83, 0.90)   # righteous-sect robe
	body.material_override = rmat
	body.position = Vector3(0.0, cy, 0.0)
	area.add_child(body)
	_add_box(area, Vector3(0.9, 0.18, 0.9), Vector3(0.0, cy - 0.1, 0.0), Color(0.55, 0.10, 0.12), Color.BLACK, false)  # sash
	_add_box(area, Vector3(0.08, 1.3, 0.08), Vector3(0.5, cy + 0.2, -0.1), Color(0.80, 0.85, 0.90), Color(0.4, 0.6, 0.9), true)  # blade
	return area

func _make_area(size: Vector3, center_y: float, is_enemy: bool) -> Area3D:
	# Collision-only trigger; callers add the visual meshes.
	var area := Area3D.new()
	var col := CollisionShape3D.new()
	var bshape := BoxShape3D.new()
	bshape.size = size
	col.shape = bshape
	col.position = Vector3(0.0, center_y, 0.0)
	area.add_child(col)
	if is_enemy:
		area.add_to_group("enemy")
	area.body_entered.connect(_on_hazard_body_entered)
	return area

func _add_box(parent: Node3D, size: Vector3, pos: Vector3, color: Color, emis: Color, emis_on: bool) -> void:
	var m := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	m.mesh = bm
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	if emis_on:
		mat.emission_enabled = true
		mat.emission = emis
	m.material_override = mat
	m.position = pos
	parent.add_child(m)

func _on_hazard_body_entered(body: Node) -> void:
	if body.is_in_group("player") and game != null:
		game.player_hit()

func _cleanup() -> void:
	var kill_z: float = player.global_position.z + DESPAWN_BEHIND
	for child in get_children():
		if child.position.z > kill_z:
			child.queue_free()
