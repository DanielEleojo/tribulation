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

const BLOCK_HEIGHT: float = 1.0
const BLOCK_DEPTH: float = 1.2
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
const FoeScript = preload("res://scripts/foe.gd")
const ENEMY_GLB := "res://Models/Enemy Animation/ninja_zombie_animated.glb"

var _enemy_scene: PackedScene

# Frequency ramp.
@export var start_interval: float = 1.4
@export var min_interval: float = 0.7
@export var ramp_time: float = 60.0
@export var gate_interval: float = 11.0   # seconds between Life/Death Gates
@export var orb_interval: float = 2.4     # seconds between Spirit Orb trails
@export var pill_interval: float = 9.0    # seconds between pill/talisman drops
const PILL_IDS := ["magnet", "double", "dash", "surge", "aegis"]

const ORB_COLOR := Color(0.55, 0.85, 1.0)   # spirit-cyan
const ORB_TRAIL := 5                          # orbs per trail
const ORB_GAP := 3.2                          # spacing along the trail

var player: Node3D
var game
var _elapsed: float = 0.0
var _timer: float = 0.0
var _gate_timer: float = 0.0
var _orb_timer: float = 0.0
var _pill_timer: float = 0.0
var _spawn_index: int = 0

func _ready() -> void:
	randomize()
	game = get_tree().get_first_node_in_group("game")
	_timer = start_interval
	_gate_timer = gate_interval
	_orb_timer = orb_interval
	_pill_timer = pill_interval
	# Enemy now uses a procedural sect-swordsman (our style) — GLB no longer loaded.

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
	_orb_timer -= delta
	if _orb_timer <= 0.0:
		_spawn_orb_trail()
		_orb_timer = orb_interval
	_pill_timer -= delta
	if _pill_timer <= 0.0:
		_spawn_pill()
		_pill_timer = pill_interval
	_cleanup()

func _current_interval() -> float:
	var t: float = clampf(_elapsed / ramp_time, 0.0, 1.0)
	return lerpf(start_interval, min_interval, t)

func _spawn() -> void:
	var base_z: float = player.global_position.z - SPAWN_AHEAD
	# In Sword-flight the road falls away: dodge aerial hazards by lane + altitude.
	if player.has_method("is_flying") and player.is_flying():
		_spawn_aerial(base_z)
		return
	# At Ascension the Heavenly Tribulation rains lightning — dodge to the safe lane.
	if game != null and game.has_method("has_ability") and game.has_ability("tribulation") and randf() < 0.55:
		_spawn_lightning(base_z)
		return
	# Cycle the three kinds so jump / slide / slash all get exercised.
	var kind: int = _spawn_index % 3
	_spawn_index += 1
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
	# A single disciple in one lane — the player goes out of their way to slay it
	# (kills push back the Heavenly Net), rather than facing an unavoidable wall.
	var lane: int = randi() % 3
	var e := _make_enemy()
	e.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, z)
	add_child(e)

## A trail of Spirit Orbs down one lane — run through them for souls + Qi (builds combo).
func _spawn_orb_trail() -> void:
	var lane: int = randi() % 3
	var x: float = float(lane - 1) * LANE_WIDTH
	var z0: float = player.global_position.z - SPAWN_AHEAD * 0.85
	for i in range(ORB_TRAIL):
		var orb := Area3D.new()
		var mesh := MeshInstance3D.new()
		var sph := SphereMesh.new()
		sph.radius = 0.35
		sph.height = 0.7
		mesh.mesh = sph
		var mat := StandardMaterial3D.new()
		mat.albedo_color = ORB_COLOR
		mat.emission_enabled = true
		mat.emission = ORB_COLOR
		mat.emission_energy_multiplier = 2.0
		mesh.material_override = mat
		mesh.position = Vector3(0.0, 1.0, 0.0)
		var col := CollisionShape3D.new()
		var sh := SphereShape3D.new()
		sh.radius = 0.6
		col.shape = sh
		col.position = Vector3(0.0, 1.0, 0.0)
		orb.add_child(mesh)
		orb.add_child(col)
		orb.add_to_group("orb")
		orb.body_entered.connect(_on_orb_hit.bind(orb))
		orb.position = Vector3(x, 0.0, z0 - float(i) * ORB_GAP)
		add_child(orb)

## Drop a single pill/talisman (a glowing gem) in a lane; pick-up activates its art.
func _spawn_pill() -> void:
	var id: String = PILL_IDS[randi() % PILL_IDS.size()]
	var col := Color(0.8, 0.9, 1.0)
	if game != null and "POWERUPS" in game and game.POWERUPS.has(id):
		col = game.POWERUPS[id]["color"]
	var lane: int = randi() % 3
	var pill := Area3D.new()
	var mesh := MeshInstance3D.new()
	var sph := SphereMesh.new()
	sph.radius = 0.45
	sph.height = 0.9
	mesh.mesh = sph
	var mat := StandardMaterial3D.new()
	mat.albedo_color = col
	mat.emission_enabled = true
	mat.emission = col
	mat.emission_energy_multiplier = 2.6
	mesh.material_override = mat
	mesh.position = Vector3(0.0, 1.2, 0.0)
	var cs := CollisionShape3D.new()
	var ss := SphereShape3D.new()
	ss.radius = 0.7
	cs.shape = ss
	cs.position = Vector3(0.0, 1.2, 0.0)
	pill.add_child(mesh)
	pill.add_child(cs)
	pill.body_entered.connect(_on_pickup.bind(pill, id))
	pill.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, player.global_position.z - SPAWN_AHEAD)
	add_child(pill)

func _on_pickup(body: Node, pill: Area3D, id: String) -> void:
	if body.is_in_group("player") and game != null and is_instance_valid(pill):
		game.activate_powerup(id)
		pill.queue_free()

func _on_orb_hit(body: Node, orb: Area3D) -> void:
	if body.is_in_group("player") and game != null and is_instance_valid(orb):
		game.on_orb_collected()
		orb.queue_free()

# Plane-coded base hues so the player reads the required dodge at a glance.
const HUE_LOW := Color(1.0, 0.5, 0.12)    # amber — ground sword-qi (JUMP)
const HUE_HIGH := Color(0.30, 0.80, 1.0)  # cyan — high blade-qi (SLIDE)

func _tier() -> Dictionary:
	if game != null and game.has_method("tier_style"):
		return game.tier_style()
	return {"accent": Color(0.8, 0.8, 0.85), "energy": 1.0, "scale": 1.0}

## Per-cultivation-stage hazard palette (jump/slide hues + foe robe).
func _hz() -> Dictionary:
	if game != null and game.has_method("hazard_style"):
		return game.hazard_style()
	return {"low": HUE_LOW, "high": HUE_HIGH, "foe": Color(0.82, 0.84, 0.92)}

func _make_barrier(is_block: bool, width: float) -> Area3D:
	var ts := _tier()
	var accent: Color = ts["accent"]
	var energy: float = ts["energy"]
	var sc: float = ts["scale"]
	var hz := _hz()
	if is_block:
		# Low ground hazard to JUMP (boulder/sweep — hue shifts per cultivation stage).
		var size := Vector3(width, BLOCK_HEIGHT, BLOCK_DEPTH)
		var cy := BLOCK_HEIGHT * 0.5
		var area := _make_area(size, cy, false)
		_add_glow(area, Vector3(width * sc, BLOCK_HEIGHT * 0.85, 0.35), Vector3(0.0, cy, 0.0), hz["low"], accent, energy)
		_add_glow(area, Vector3(width * sc, 0.14, 0.7), Vector3(0.0, 0.07, 0.0), hz["low"], accent, energy * 1.4)  # bright ground line
		return area
	else:
		# High hazard to SLIDE under (branch/blade — hue shifts per stage).
		var size := Vector3(width, BAR_HEIGHT, BAR_DEPTH)
		var cy := BAR_BOTTOM_Y + BAR_HEIGHT * 0.5
		var area := _make_area(size, cy, false)
		_add_glow(area, Vector3(width * sc, BAR_HEIGHT, 0.3), Vector3(0.0, cy, 0.0), hz["high"], accent, energy)
		return area

func _make_enemy() -> Area3D:
	# Sect disciple — a procedural robed swordsman in our primitive/glowing-qi style,
	# matching the player figure. Robe whitens (righteous) vs the demon's dark; rank-qi
	# tints the robe and lights the jian; loftier ranks loom larger and glow brighter.
	var ts := _tier()
	var accent: Color = ts["accent"]
	var energy: float = ts["energy"]
	var sc: float = ts["scale"]
	var area := _make_area(ENEMY_SIZE, ENEMY_SIZE.y * 0.5, true)
	var f := Node3D.new()
	f.set_script(FoeScript)                  # running bob/sway
	f.scale = Vector3(sc, sc, sc)
	area.add_child(f)

	var robe: Color = Color(_hz()["foe"]).lerp(accent, 0.22)   # foe identity shifts per stage
	# Torso (robe capsule) + flared lower robe.
	var body := MeshInstance3D.new()
	var cap := CapsuleMesh.new()
	cap.radius = 0.42
	cap.height = 1.7
	body.mesh = cap
	body.material_override = _solid(robe)
	body.position = Vector3(0.0, 1.15, 0.0)
	f.add_child(body)
	_add_box(f, Vector3(1.0, 0.8, 0.8), Vector3(0.0, 0.45, 0.0), robe.darkened(0.12), Color.BLACK, false)   # skirt
	# Head + topknot.
	var head := MeshInstance3D.new()
	var hs := SphereMesh.new()
	hs.radius = 0.27
	hs.height = 0.54
	head.mesh = hs
	head.material_override = _solid(Color(0.86, 0.72, 0.62))   # skin
	head.position = Vector3(0.0, 2.12, 0.0)
	f.add_child(head)
	_add_box(f, Vector3(0.2, 0.24, 0.2), Vector3(0.0, 2.42, 0.0), Color(0.10, 0.08, 0.07), Color.BLACK, false)   # topknot
	# Waist sash.
	_add_box(f, Vector3(0.95, 0.18, 0.95), Vector3(0.0, 1.02, 0.0), Color(0.55, 0.12, 0.14), Color.BLACK, false)
	# Glowing jian held at the side (rank-qi blade).
	_add_glow(f, Vector3(0.08, 1.5, 0.08), Vector3(0.52, 1.45, -0.1), Color(0.85, 0.88, 0.95), accent, energy * 1.2)
	return area

func _solid(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	return m

## Heavenly Tribulation: lightning strikes two of the three lanes; one lane is safe.
func _spawn_lightning(z: float) -> void:
	var ts := _tier()
	var accent: Color = ts["accent"]
	var energy: float = ts["energy"]
	var safe: int = randi() % 3
	for lane in range(3):
		if lane == safe:
			continue
		var size := Vector3(0.7, 7.0, 0.7)
		var bolt := _make_area(size, size.y * 0.5, false)   # full-height column, lethal on contact
		bolt.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, z)
		_add_glow(bolt, Vector3(0.45, 7.0, 0.45), Vector3(0.0, size.y * 0.5, 0.0), Color(0.75, 0.88, 1.0), Color(1.0, 0.95, 0.7), energy * 1.6)
		add_child(bolt)

## A floating sword-formation hazard at flight altitude — dodge by lane + climb/dive.
func _spawn_aerial(z: float) -> void:
	var ts := _tier()
	var accent: Color = ts["accent"]
	var energy: float = ts["energy"]
	var lane: int = randi() % 3
	var x: float = float(lane - 1) * LANE_WIDTH
	var y: float = randf_range(2.6, 5.2)
	var size := Vector3(2.0, 1.6, 1.0)
	var area := _make_area(size, 0.0, false)
	area.position = Vector3(x, y, z)
	_add_glow(area, size, Vector3.ZERO, HUE_HIGH, accent, energy)
	add_child(area)

func _find_anim(root: Node) -> AnimationPlayer:
	var f := root.find_children("*", "AnimationPlayer", true, false)
	return f[0] if f.size() > 0 else null

func _local_aabb(root: Node) -> AABB:
	var box := AABB()
	for m in root.find_children("*", "MeshInstance3D", true, false):
		var a: AABB = (m as MeshInstance3D).get_aabb()
		box = a if box.size == Vector3.ZERO else box.merge(a)
	return box

## Lock horizontal drift of the root/hip position track (keep vertical bounce).
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
		if (maxx - minx) < 0.15 and (maxz - minz) < 0.15:
			continue
		var base_v: Vector3 = anim.track_get_key_value(i, 0)
		for k in range(n):
			var v: Vector3 = anim.track_get_key_value(i, k)
			anim.track_set_key_value(i, k, Vector3(base_v.x, v.y, base_v.z))

## A glowing energy mesh: dark base hue, emission pushed toward the rank's qi color.
func _add_glow(parent: Node3D, size: Vector3, pos: Vector3, hue: Color, accent: Color, energy: float) -> void:
	var m := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	m.mesh = bm
	var mat := StandardMaterial3D.new()
	mat.albedo_color = hue.darkened(0.3)
	mat.emission_enabled = true
	mat.emission = hue.lerp(accent, 0.45)
	mat.emission_energy_multiplier = energy
	m.material_override = mat
	m.position = pos
	parent.add_child(m)

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
	area.body_entered.connect(_on_hazard_body_entered.bind(area))
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

func _on_hazard_body_entered(body: Node, area: Area3D) -> void:
	if not body.is_in_group("player") or game == null:
		return
	# Sword-Qi Dash plows straight through hazards/foes instead of being felled.
	if game.has_method("is_powerup_active") and game.is_powerup_active("dash"):
		if is_instance_valid(area):
			area.queue_free()
		return
	game.player_hit()

func _cleanup() -> void:
	var kill_z: float = player.global_position.z + DESPAWN_BEHIND
	for child in get_children():
		if child.position.z > kill_z:
			child.queue_free()
