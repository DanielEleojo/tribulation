extends Node3D
## Infinite ground via RECYCLING tiles along Z (forward = -Z). Each tile is a stone
## RUNWAY (the 3-lane path) on terrain shoulders, with glowing lane-divider + edge
## lines and scrolling cross-rungs for speed readability. Shared materials let a
## theme change (forest / sect / hellscape) recolor every tile at once.

const TILE_WIDTH: float = 12.0
const TILE_LENGTH: float = 20.0
const TILE_COUNT: int = 10
const RECYCLE_BEHIND: float = 25.0
const LANE_WIDTH: float = 2.5                # lanes at -2.5, 0, +2.5
const PATH_WIDTH: float = LANE_WIDTH * 3.0   # the runway spans all three lanes (7.5)
const RUNG_SPACING: float = 5.0              # cross-bar spacing along the path

var player: Node3D
var tiles: Array[StaticBody3D] = []
# Shared materials (recolored by set_theme).
var _mat_se: StandardMaterial3D     # shoulder terrain (even tiles)
var _mat_so: StandardMaterial3D     # shoulder terrain (odd tiles)
var _mat_path: StandardMaterial3D   # the stone runway
var _mat_line: StandardMaterial3D   # glowing lane dividers + road edges
var _mat_rung: StandardMaterial3D   # dim glowing cross-rungs

func _ready() -> void:
	_mat_se = StandardMaterial3D.new()
	_mat_so = StandardMaterial3D.new()
	_mat_path = StandardMaterial3D.new()
	_mat_line = StandardMaterial3D.new()
	_mat_line.emission_enabled = true
	_mat_line.emission_energy_multiplier = 1.6
	_mat_rung = StandardMaterial3D.new()
	_mat_rung.emission_enabled = true
	_mat_rung.emission_energy_multiplier = 0.6
	set_theme(Color(0.20, 0.16, 0.14), Color(0.16, 0.13, 0.12), Color(0.13, 0.12, 0.10), Color(0.55, 0.60, 0.40))
	for i in range(TILE_COUNT):
		var t := _make_tile(i)
		t.position.z = -float(i) * TILE_LENGTH
		add_child(t)
		tiles.append(t)

## Recolor for an environment theme: terrain shoulders (even/odd), stone path, and
## the glowing accent for lane/edge lines + rungs.
func set_theme(shoulder_even: Color, shoulder_odd: Color, path: Color, accent: Color) -> void:
	_mat_se.albedo_color = shoulder_even
	_mat_so.albedo_color = shoulder_odd
	_mat_path.albedo_color = path
	_mat_line.albedo_color = accent
	_mat_line.emission = accent
	_mat_rung.albedo_color = accent.darkened(0.35)
	_mat_rung.emission = accent

func _make_tile(idx: int) -> StaticBody3D:
	var body := StaticBody3D.new()

	# Shoulders: the full-width base slab (top at y=0), alternating shade.
	var slab := MeshInstance3D.new()
	var sbox := BoxMesh.new()
	sbox.size = Vector3(TILE_WIDTH, 1.0, TILE_LENGTH)
	slab.mesh = sbox
	slab.position = Vector3(0.0, -0.5, 0.0)
	slab.material_override = _mat_se if idx % 2 == 0 else _mat_so
	body.add_child(slab)

	# Collision = the full slab top.
	var col := CollisionShape3D.new()
	var cshape := BoxShape3D.new()
	cshape.size = Vector3(TILE_WIDTH, 1.0, TILE_LENGTH)
	col.shape = cshape
	col.position = Vector3(0.0, -0.5, 0.0)
	body.add_child(col)

	# Stone runway down the middle, just proud of the shoulders.
	_strip(body, Vector3(PATH_WIDTH, 0.5, TILE_LENGTH), Vector3(0.0, -0.24, 0.0), _mat_path)

	# Glowing road edges (frame the runway) + lane dividers.
	var edge := PATH_WIDTH * 0.5 - 0.1
	_strip(body, Vector3(0.22, 0.5, TILE_LENGTH), Vector3(-edge, -0.22, 0.0), _mat_line)
	_strip(body, Vector3(0.22, 0.5, TILE_LENGTH), Vector3(edge, -0.22, 0.0), _mat_line)
	for sx in [-LANE_WIDTH * 0.5, LANE_WIDTH * 0.5]:
		_strip(body, Vector3(0.10, 0.5, TILE_LENGTH), Vector3(sx, -0.22, 0.0), _mat_line)

	# Scrolling cross-rungs across the path (motion readability).
	var z := -TILE_LENGTH * 0.5 + RUNG_SPACING * 0.5
	while z < TILE_LENGTH * 0.5:
		_strip(body, Vector3(PATH_WIDTH, 0.5, 0.16), Vector3(0.0, -0.23, z), _mat_rung)
		z += RUNG_SPACING

	return body

func _strip(parent: Node3D, size: Vector3, pos: Vector3, mat: StandardMaterial3D) -> void:
	var m := MeshInstance3D.new()
	var b := BoxMesh.new()
	b.size = size
	m.mesh = b
	m.position = pos
	m.material_override = mat
	parent.add_child(m)

func _physics_process(_delta: float) -> void:
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	var behind_z: float = player.global_position.z + RECYCLE_BEHIND
	for t in tiles:
		if t.position.z > behind_z:
			t.position.z = _frontmost_z() - TILE_LENGTH

func _frontmost_z() -> float:
	var m := INF
	for t in tiles:
		m = min(m, t.position.z)
	return m
