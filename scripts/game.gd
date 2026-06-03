extends Node3D
## Root coordinator: owns dead/alive state, wires death + restart, and sets up
## the 3D world (lighting + environment) in code so we don't hand-author resources.
## Wiring happens here because the root readies LAST (every child already exists).

signal died
signal qi_changed(qi: float, qi_max: float)
signal net_changed(net: float)

@export var qi_max: float = 100.0      # Qi needed to trigger a Qi Burst
@export var qi_per_kill: float = 20.0  # Qi gained per enemy slain (5 kills = burst)

@export var net_close_rate: float = 0.045   # how fast the Heavenly Net closes (per sec)
@export var net_push_per_kill: float = 0.12 # how much a kill pushes the net back
@export var net_burst_relief: float = 0.30  # extra net relief from a Qi Burst

var is_dead: bool = false
var qi: float = 0.0
var net: float = 0.0                  # 0 = open, 1 = closed (death)

func _ready() -> void:
	add_to_group("game")
	_setup_world()

	var player := get_tree().get_first_node_in_group("player")
	var hud := get_tree().get_first_node_in_group("hud")
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if player != null:
		died.connect(player.on_death)
	if hud != null:
		died.connect(hud.on_death)
		qi_changed.connect(hud.on_qi_changed)
	if swipe != null:
		swipe.tapped.connect(_on_tap)
	var net_overlay := get_tree().get_first_node_in_group("net_overlay")
	if net_overlay != null:
		net_changed.connect(net_overlay.on_net_changed)

	qi_changed.emit(qi, qi_max)   # initialize the HUD bar at 0
	net_changed.emit(net)

func _setup_world() -> void:
	# Environment: dark color background + soft ambient + distance fog (hides the
	# far edge of the ground and the spawn point of obstacles).
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.10, 0.10, 0.14)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.55, 0.55, 0.65)
	env.ambient_light_energy = 0.6
	env.fog_enabled = true
	env.fog_light_color = Color(0.10, 0.10, 0.14)
	env.fog_density = 0.012
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

	# Key light from above/ahead so the boxes get readable shading.
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50.0, -35.0, 0.0)
	sun.light_energy = 1.1
	sun.shadow_enabled = true
	add_child(sun)

func _process(delta: float) -> void:
	# Restart (Enter) only acts on the death screen.
	if is_dead:
		if Input.is_action_just_pressed("restart"):
			restart()
		return
	# The Heavenly Net steadily closes; full closure is death.
	net = minf(1.0, net + net_close_rate * delta)
	net_changed.emit(net)
	if net >= 1.0:
		die()

## Called by an obstacle when it touches the player.
func die() -> void:
	if is_dead:
		return
	is_dead = true
	died.emit()

## Called by the player after a slash kills enemies. Charges Qi; bursts at max.
func on_enemy_killed(count: int = 1) -> void:
	if is_dead:
		return
	qi = minf(qi_max, qi + qi_per_kill * float(count))
	qi_changed.emit(qi, qi_max)
	# Each kill pushes the Heavenly Net back.
	net = maxf(0.0, net - net_push_per_kill * float(count))
	net_changed.emit(net)
	if qi >= qi_max:
		_qi_burst()

## Qi Burst: clear every enemy on the field, flash a shockwave, reset Qi.
func _qi_burst() -> void:
	for e in get_tree().get_nodes_in_group("enemy"):
		if is_instance_valid(e):
			e.queue_free()
	_spawn_burst_fx()
	qi = 0.0
	qi_changed.emit(qi, qi_max)
	# A burst also throws the Heavenly Net back.
	net = maxf(0.0, net - net_burst_relief)
	net_changed.emit(net)

func _spawn_burst_fx() -> void:
	var p := get_tree().get_first_node_in_group("player")
	if p == null:
		return
	var fx := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	fx.mesh = sphere
	var m := StandardMaterial3D.new()
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.albedo_color = Color(0.6, 0.85, 1.0, 0.5)
	m.emission_enabled = true
	m.emission = Color(0.5, 0.8, 1.0)
	fx.material_override = m
	fx.position = Vector3(0.0, 1.0, 0.0)
	p.add_child(fx)
	var tw := fx.create_tween()
	tw.set_parallel(true)
	tw.tween_property(fx, "scale", Vector3(14.0, 14.0, 14.0), 0.45)
	tw.tween_property(m, "albedo_color:a", 0.0, 0.45)
	tw.chain().tween_callback(fx.queue_free)

func _on_tap() -> void:
	if is_dead:
		restart()

func restart() -> void:
	get_tree().reload_current_scene()
