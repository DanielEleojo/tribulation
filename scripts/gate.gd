extends Node3D
## Life/Death Gate: three lane curtains the player passes through. One lane is the
## green Life Gate (safe); the other two are red Death Gates. The player steers
## into the green lane. Curtains are translucent Area3D triggers (non-blocking) so
## a wrong choice is a penalty, not an instant wall — readable, non-lethal early.

const LANE_WIDTH: float = 2.5
const PANEL_W: float = 2.4
const PANEL_H: float = 4.0
const PANEL_D: float = 0.4
const SAFE_COLOR := Color(0.2, 0.9, 0.4, 0.45)
const DEATH_COLOR := Color(0.9, 0.15, 0.2, 0.5)

var _resolved: bool = false
var _game

## Build the three curtains; safe_lane (0/1/2) is the Life Gate.
func setup(safe_lane: int, game_ref) -> void:
	_game = game_ref
	for lane in range(3):
		var panel := _make_panel(lane == safe_lane)
		panel.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, 0.0)
		add_child(panel)

func _make_panel(safe: bool) -> Area3D:
	var area := Area3D.new()
	area.set_meta("safe", safe)
	var mesh := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(PANEL_W, PANEL_H, PANEL_D)
	mesh.mesh = box
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = SAFE_COLOR if safe else DEATH_COLOR
	mat.emission_enabled = true
	mat.emission = Color(0.1, 0.7, 0.2) if safe else Color(0.7, 0.05, 0.1)
	mesh.material_override = mat
	mesh.position = Vector3(0.0, PANEL_H * 0.5, 0.0)
	var col := CollisionShape3D.new()
	var bs := BoxShape3D.new()
	bs.size = Vector3(PANEL_W, PANEL_H, PANEL_D)
	col.shape = bs
	col.position = Vector3(0.0, PANEL_H * 0.5, 0.0)
	area.add_child(mesh)
	area.add_child(col)

	# Torii frame: two posts + a lintel beam, plus a bright life/death talisman.
	var post_color := Color(0.24, 0.10, 0.07)
	_gate_box(area, Vector3(0.25, PANEL_H, 0.25), Vector3(-PANEL_W * 0.5, PANEL_H * 0.5, 0.0), post_color, Color.BLACK, false)
	_gate_box(area, Vector3(0.25, PANEL_H, 0.25), Vector3(PANEL_W * 0.5, PANEL_H * 0.5, 0.0), post_color, Color.BLACK, false)
	_gate_box(area, Vector3(PANEL_W + 0.7, 0.35, 0.4), Vector3(0.0, PANEL_H + 0.1, 0.0), Color(0.45, 0.12, 0.09), Color.BLACK, false)
	var tcol := Color(0.3, 1.0, 0.4) if safe else Color(1.0, 0.2, 0.2)
	_gate_box(area, Vector3(0.5, 0.9, 0.12), Vector3(0.0, PANEL_H * 0.55, -PANEL_D * 0.5 - 0.05), tcol, tcol, true)

	area.body_entered.connect(_on_panel_entered.bind(area))
	return area

func _gate_box(parent: Node3D, size: Vector3, pos: Vector3, color: Color, emis: Color, emis_on: bool) -> void:
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

func _on_panel_entered(body: Node, area: Area3D) -> void:
	if _resolved or not body.is_in_group("player"):
		return
	_resolved = true
	if _game != null:
		_game.on_gate(area.get_meta("safe"))
	queue_free()
