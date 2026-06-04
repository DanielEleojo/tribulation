extends Node3D
## Infinite ground via RECYCLING tiles along the Z axis (forward = -Z).
## Tiles are laid end-to-end ahead of the player; when a tile falls behind the
## player it is moved to the front of the line so the ground never shows a gap.
## Lane divider lines + alternating tile shades make the forward motion readable.

const TILE_WIDTH: float = 12.0
const TILE_LENGTH: float = 20.0
const TILE_COUNT: int = 10
const RECYCLE_BEHIND: float = 25.0     # how far behind the player before a tile recycles
const LANE_WIDTH: float = 2.5          # lane spacing (lanes at -2.5, 0, +2.5)

var player: Node3D
var tiles: Array[StaticBody3D] = []
# Shared materials so a theme change instantly recolors all tiles (existing + future).
var _mat_even: StandardMaterial3D
var _mat_odd: StandardMaterial3D
var _mat_div: StandardMaterial3D

func _ready() -> void:
	_mat_even = StandardMaterial3D.new()
	_mat_even.albedo_color = Color(0.20, 0.16, 0.14)
	_mat_odd = StandardMaterial3D.new()
	_mat_odd.albedo_color = Color(0.16, 0.13, 0.12)
	_mat_div = StandardMaterial3D.new()
	_mat_div.albedo_color = Color(0.5, 0.55, 0.42)
	for i in range(TILE_COUNT):
		var t := _make_tile(i)
		t.position.z = -float(i) * TILE_LENGTH
		add_child(t)
		tiles.append(t)

## Recolor the ground for an environment theme (forest / sect / hellscape).
func set_theme(even: Color, odd: Color, divider: Color) -> void:
	_mat_even.albedo_color = even
	_mat_odd.albedo_color = odd
	_mat_div.albedo_color = divider

func _make_tile(idx: int) -> StaticBody3D:
	var body := StaticBody3D.new()

	# Tile slab: top surface sits at y=0.
	var mesh := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(TILE_WIDTH, 1.0, TILE_LENGTH)
	mesh.mesh = box
	mesh.position = Vector3(0.0, -0.5, 0.0)
	mesh.material_override = _mat_even if idx % 2 == 0 else _mat_odd
	body.add_child(mesh)

	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(TILE_WIDTH, 1.0, TILE_LENGTH)
	col.shape = shape
	col.position = Vector3(0.0, -0.5, 0.0)
	body.add_child(col)

	# Lane divider lines between the three lanes (boundaries at +/- LANE_WIDTH/2).
	for sx in [-LANE_WIDTH * 0.5, LANE_WIDTH * 0.5]:
		var line := MeshInstance3D.new()
		var lbox := BoxMesh.new()
		lbox.size = Vector3(0.12, 0.06, TILE_LENGTH)
		line.mesh = lbox
		line.position = Vector3(sx, 0.03, 0.0)
		line.material_override = _mat_div
		body.add_child(line)

	return body

func _physics_process(_delta: float) -> void:
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	var behind_z: float = player.global_position.z + RECYCLE_BEHIND
	for t in tiles:
		# A tile whose center is well behind the player gets moved to the front.
		if t.position.z > behind_z:
			t.position.z = _frontmost_z() - TILE_LENGTH

func _frontmost_z() -> float:
	var m := INF
	for t in tiles:
		m = min(m, t.position.z)
	return m
