extends Node3D
## Roadside scenery: recycled CC0 props (Poly Pizza) lining both shoulders of the
## path. Theme-aware — forest props while fleeing the woods, sect props (lanterns,
## torii) once on the sect's home turf. Purely decorative (no collision).

const PROPS_FOREST: Array[String] = ["tree", "pine", "rock"]
const PROPS_SECT: Array[String] = ["lantern", "torii", "rock"]
const TARGET_H := {"tree": 6.0, "pine": 7.5, "lantern": 2.6, "torii": 5.5, "rock": 1.8}
const PER_SIDE: int = 8
const SPACING: float = 11.0
const RECYCLE_BEHIND: float = 20.0

var _scenes: Dictionary = {}
var player: Node3D
var game
var _slots: Array = []   # {node, side}

func _ready() -> void:
	randomize()
	for n in ["tree", "pine", "lantern", "torii", "rock"]:
		_scenes[n] = load("res://assets/props/%s.glb" % n)
	game = get_tree().get_first_node_in_group("game")
	for i in range(PER_SIDE):
		_make_slot(-1, -float(i) * SPACING)
		_make_slot(1, -float(i) * SPACING - SPACING * 0.5)   # stagger the two sides

func _make_slot(side: int, z: float) -> void:
	var holder := Node3D.new()
	add_child(holder)
	holder.position = Vector3(0.0, 0.0, z)
	holder.set_meta("side", side)
	_slots.append(holder)
	_populate(holder)

func _populate(holder: Node3D) -> void:
	for c in holder.get_children():
		c.queue_free()
	var set := _current_set()
	var pname: String = set[randi() % set.size()]
	var scene = _scenes.get(pname)
	if scene == null:
		return
	var inst = scene.instantiate()
	holder.add_child(inst)
	var side: int = holder.get_meta("side")
	holder.position.x = float(side) * randf_range(7.5, 12.0)
	inst.rotation.y = randf() * TAU
	_normalize(inst, float(TARGET_H.get(pname, 4.0)))

func _current_set() -> Array:
	if game != null and game.realm <= 1:
		return PROPS_FOREST
	return PROPS_SECT

## Scale a model to a target height and sit it on the ground (min Y = 0).
func _normalize(inst: Node3D, target_h: float) -> void:
	var aabb := _calc_aabb(inst)
	if aabb.size.y <= 0.001:
		return
	var s := target_h / aabb.size.y
	inst.scale = Vector3(s, s, s)
	inst.position.y = -aabb.position.y * s

func _calc_aabb(root: Node3D) -> AABB:
	var out := AABB()
	var first := true
	for m in root.find_children("*", "MeshInstance3D", true, false):
		var local: Transform3D = root.global_transform.affine_inverse() * m.global_transform
		var xa: AABB = local * m.get_aabb()
		if first:
			out = xa
			first = false
		else:
			out = out.merge(xa)
	return out

func _physics_process(_delta: float) -> void:
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	var behind_z: float = player.global_position.z + RECYCLE_BEHIND
	for holder in _slots:
		if holder.position.z > behind_z:
			holder.position.z = _frontmost_z() - SPACING
			_populate(holder)

func _frontmost_z() -> float:
	var m := INF
	for holder in _slots:
		m = minf(m, holder.position.z)
	return m
