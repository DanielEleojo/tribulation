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
@export var hard_min_interval: float = 0.42  # ENDLESS density floor (no plateau, but bounded)
@export var endless_ramp: float = 200.0      # seconds past ramp_time to reach the density floor
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

# Object pools — reuse spawned nodes instead of alloc/free each frame (fewer GC hitches
# at the dense end of the curve). Pooled nodes stay parented but hidden + inactive,
# tagged via the "pkind" meta; _cleanup skips anything flagged "inactive".
var _pool: Dictionary = {}            # kind -> Array of inactive nodes
var _dbg_created: Dictionary = {}     # kind -> lifetime nodes built (instrumentation)
var _dbg_reused: Dictionary = {}      # kind -> lifetime nodes reused from pool

## Take an inactive node of this kind from the pool, or null if none free.
func _acquire(kind: String):
	var arr: Array = _pool.get(kind, [])
	while not arr.is_empty():
		var n = arr.pop_back()
		if is_instance_valid(n):
			n.set_meta("inactive", false)
			_dbg_reused[kind] = _dbg_reused.get(kind, 0) + 1
			return n
	return null

## Park a node back in its pool: hide it, stop it sensing, shed per-kind state.
func _release(kind: String, n: Node) -> void:
	n.set_meta("inactive", true)
	if n is Node3D:
		(n as Node3D).visible = false
	if n is Area3D:
		(n as Area3D).monitoring = false
	if kind == "orb":
		n.remove_from_group("orb")
	elif kind == "haz":
		if n.has_meta("lesson"):
			n.remove_meta("lesson")   # don't let a stale tutorial tag follow a reused shell
		var kids := n.get_children()
		for i in range(kids.size() - 1, 0, -1):   # keep child 0 (the CollisionShape)
			kids[i].free()
	if not _pool.has(kind):
		_pool[kind] = []
	_pool[kind].append(n)

## Retire a live node: pool it if poolable (pkind meta), else free it.
func _retire(n: Node) -> void:
	if not is_instance_valid(n):
		return
	var k: String = String(n.get_meta("pkind", ""))
	if k != "":
		_release(k, n)
	else:
		n.queue_free()

func _ready() -> void:
	randomize()
	add_to_group("spawner")
	game = get_tree().get_first_node_in_group("game")
	# Pull cadence from the Balance autoload (falls back to the @export values).
	start_interval = Balance.getf("spawn_start_interval", start_interval)
	min_interval = Balance.getf("spawn_min_interval", min_interval)
	ramp_time = Balance.getf("spawn_ramp_time", ramp_time)
	hard_min_interval = Balance.getf("spawn_hard_min_interval", hard_min_interval)
	endless_ramp = Balance.getf("spawn_endless_ramp", endless_ramp)
	gate_interval = Balance.getf("spawn_gate_interval", gate_interval)
	orb_interval = Balance.getf("spawn_orb_interval", orb_interval)
	pill_interval = Balance.getf("spawn_pill_interval", pill_interval)
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
	var i: float = lerpf(start_interval, min_interval, t)
	# Past the ramp the road never settles — hazards keep crowding in until the floor.
	if _elapsed > ramp_time:
		var e: float = clampf((_elapsed - ramp_time) / endless_ramp, 0.0, 1.0)
		i = lerpf(min_interval, hard_min_interval, e)
	return i

## Begin a run; higher realms start deeper into the difficulty curve.
func begin_run(difficulty_offset: float = 0.0) -> void:
	_elapsed = difficulty_offset

func _spawn() -> void:
	var base_z: float = player.global_position.z - SPAWN_AHEAD
	# Heavenly Tribulation: relentless lightning while you endure the breakthrough.
	if game != null and game.has_method("in_tribulation") and game.in_tribulation():
		_spawn_lightning(base_z)
		return
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
	var lesson := "jump" if is_block else "slide"   # tutorial cue (Stone Ward / Spirit Barrier)
	if randf() < 0.5:
		# Full-width: lane-switching can't save you, the action is forced.
		var obs := _make_barrier(is_block, FULL_WIDTH)
		obs.position = Vector3(0.0, 0.0, z)
		obs.set_meta("lesson", lesson)
	else:
		# Single lane: dodge to another lane, or perform the action.
		var lane: int = randi() % 3
		var w: float = BLOCK_LANE_WIDTH if is_block else BAR_LANE_WIDTH
		var obs := _make_barrier(is_block, w)
		obs.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, z)
		obs.set_meta("lesson", lesson)

func _spawn_gate() -> void:
	var safe_lane: int = randi() % 3
	var gate := GateScript.new()
	gate.position = Vector3(0.0, 0.0, player.global_position.z - SPAWN_AHEAD)
	gate.set_meta("lesson", "gate")
	add_child(gate)
	gate.setup(safe_lane, game)

func _spawn_enemy_row(z: float) -> void:
	# A single disciple in one lane — the player goes out of their way to slay it
	# (kills push back the Heavenly Net), rather than facing an unavoidable wall.
	var lane: int = randi() % 3
	var e := _make_enemy()
	e.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, z)
	e.set_meta("lesson", "slash")

## A trail of Spirit Orbs down one lane — run through them for souls + Qi (builds combo).
func _spawn_orb_trail() -> void:
	var lane: int = randi() % 3
	var x: float = float(lane - 1) * LANE_WIDTH
	var z0: float = player.global_position.z - SPAWN_AHEAD * 0.85
	for i in range(ORB_TRAIL):
		var orb := _obtain_orb()
		orb.position = Vector3(x, 0.0, z0 - float(i) * ORB_GAP)


func _build_orb() -> Area3D:
	var orb := Area3D.new()
	orb.set_meta("pkind", "orb")
	orb.set_meta("lesson", "orb")   # tutorial cue (constant across pooled reuse)
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
	orb.body_entered.connect(_on_orb_hit.bind(orb))
	_dbg_created["orb"] = _dbg_created.get("orb", 0) + 1
	return orb


func _obtain_orb() -> Area3D:
	var orb: Area3D = _acquire("orb")
	if orb == null:
		orb = _build_orb()
	if orb.get_parent() == null:
		add_child(orb)
	orb.add_to_group("orb")
	orb.visible = true
	orb.monitoring = true
	return orb

## Drop a single pill/talisman (a glowing gem) in a lane; pick-up activates its art.
func _spawn_pill() -> void:
	var id: String = PILL_IDS[randi() % PILL_IDS.size()]
	var col := Color(0.8, 0.9, 1.0)
	if game != null and "POWERUPS" in game and game.POWERUPS.has(id):
		col = game.POWERUPS[id]["color"]
	var lane: int = randi() % 3
	var pill := _obtain_pill()
	pill.set_meta("pid", id)
	var mat := (pill.get_child(0) as MeshInstance3D).material_override as StandardMaterial3D
	mat.albedo_color = col
	mat.emission = col
	pill.position = Vector3(float(lane - 1) * LANE_WIDTH, 0.0, player.global_position.z - SPAWN_AHEAD)


func _build_pill() -> Area3D:
	var pill := Area3D.new()
	pill.set_meta("pkind", "pill")
	var mesh := MeshInstance3D.new()
	var sph := SphereMesh.new()
	sph.radius = 0.45
	sph.height = 0.9
	mesh.mesh = sph
	var mat := StandardMaterial3D.new()
	mat.emission_enabled = true
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
	pill.body_entered.connect(_on_pickup.bind(pill))
	_dbg_created["pill"] = _dbg_created.get("pill", 0) + 1
	return pill


func _obtain_pill() -> Area3D:
	var pill: Area3D = _acquire("pill")
	if pill == null:
		pill = _build_pill()
	if pill.get_parent() == null:
		add_child(pill)
	pill.visible = true
	pill.monitoring = true
	return pill


func _on_pickup(body: Node, pill: Area3D) -> void:
	if body.is_in_group("player") and game != null and is_instance_valid(pill):
		game.activate_powerup(String(pill.get_meta("pid", "")))
		call_deferred("_retire", pill)   # deferred: safe to toggle state outside the signal

func _on_orb_hit(body: Node, orb: Area3D) -> void:
	if body.is_in_group("player") and game != null and is_instance_valid(orb):
		game.on_orb_collected()
		call_deferred("_retire", orb)

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
		# STONE WARD to JUMP — a rough rune-carved stone with a jagged crest.
		var size := Vector3(width, BLOCK_HEIGHT, BLOCK_DEPTH)
		var cy := BLOCK_HEIGHT * 0.5
		var area := _make_area(size, cy, false)
		var stone := Color(0.30, 0.29, 0.33)
		_add_box(area, Vector3(width, BLOCK_HEIGHT, BLOCK_DEPTH), Vector3(0.0, cy, 0.0), stone, Color.BLACK, false)
		# jagged crest along the top so it isn't a flat slab
		var n := maxi(2, int(width / 1.6))
		for i in range(n):
			var fx := (float(i) / float(maxi(1, n - 1)) - 0.5) * (width - 0.5)
			var hh := 0.30 + 0.22 * float((i % 2))
			_add_box(area, Vector3((width / float(n)) * 0.7, hh, BLOCK_DEPTH * 0.7), Vector3(fx, BLOCK_HEIGHT + hh * 0.5, 0.0), stone.darkened(0.12), Color.BLACK, false)
		# glowing rune carved on the face + a qi-line at the base
		_add_glow(area, Vector3(minf(width, 1.4) * sc, 0.55, 0.08), Vector3(0.0, cy + 0.05, -BLOCK_DEPTH * 0.5 - 0.05), hz["low"], accent, energy * 1.6)
		_add_glow(area, Vector3(width * sc, 0.12, BLOCK_DEPTH * 0.7), Vector3(0.0, 0.06, 0.0), hz["low"], accent, energy * 1.3)
		return area
	else:
		# SPIRIT BARRIER to SLIDE under — posts + lintel + a humming qi beam hung with talismans.
		var size := Vector3(width, BAR_HEIGHT, BAR_DEPTH)
		var cy := BAR_BOTTOM_Y + BAR_HEIGHT * 0.5
		var area := _make_area(size, cy, false)
		var wood := Color(0.26, 0.18, 0.12)
		var post_top := cy + BAR_HEIGHT * 0.5 + 0.5
		for sx in [-width * 0.5 + 0.12, width * 0.5 - 0.12]:
			_add_box(area, Vector3(0.18, post_top, 0.18), Vector3(sx, post_top * 0.5, 0.0), wood, Color.BLACK, false)
		_add_box(area, Vector3(width + 0.25, 0.18, 0.28), Vector3(0.0, post_top, 0.0), wood.lightened(0.1), Color.BLACK, false)
		# the lethal glowing beam
		_add_glow(area, Vector3(width * sc, BAR_HEIGHT, BAR_DEPTH), Vector3(0.0, cy, 0.0), hz["high"], accent, energy * 1.4)
		# hanging talisman strips dangling into view
		var t := maxi(2, int(width / 1.6))
		for i in range(t):
			var tx := (float(i) / float(maxi(1, t - 1)) - 0.5) * (width - 0.6)
			_add_glow(area, Vector3(0.13, 0.45, 0.04), Vector3(tx, cy - BAR_HEIGHT * 0.5 - 0.22, -BAR_DEPTH * 0.5), hz["high"].lerp(Color(1.0, 0.3, 0.2), 0.4), accent, energy)
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

## Collision-only trigger; callers add the visual meshes. Non-enemy shells are pooled
## (their box-collision + signal are reused; visuals are rebuilt each spawn). Enemies
## are not pooled — they're freed externally by slash / Qi Burst.
func _make_area(size: Vector3, center_y: float, is_enemy: bool) -> Area3D:
	var area: Area3D = null
	if not is_enemy:
		area = _acquire("haz")
	if area == null:
		area = _new_area_shell(not is_enemy)
	var col := area.get_child(0) as CollisionShape3D
	(col.shape as BoxShape3D).size = size
	col.position = Vector3(0.0, center_y, 0.0)
	if is_enemy:
		area.add_to_group("enemy")
	if area.get_parent() == null:
		add_child(area)
	area.visible = true
	area.monitoring = true
	return area

func _new_area_shell(pooled: bool) -> Area3D:
	var area := Area3D.new()
	var col := CollisionShape3D.new()
	col.shape = BoxShape3D.new()
	area.add_child(col)
	area.body_entered.connect(_on_hazard_body_entered.bind(area))
	if pooled:
		area.set_meta("pkind", "haz")
		_dbg_created["haz"] = _dbg_created.get("haz", 0) + 1
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
			call_deferred("_retire", area)
		return
	game.player_hit()

func _cleanup() -> void:
	var kill_z: float = player.global_position.z + DESPAWN_BEHIND
	for child in get_children():
		if child.get_meta("inactive", false):
			continue                       # parked in a pool — not in play
		if child.position.z > kill_z:
			_retire(child)
