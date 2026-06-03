extends Node3D
## Root coordinator: owns dead/alive state, wires death + restart, and sets up
## the 3D world (lighting + environment) in code so we don't hand-author resources.
## Wiring happens here because the root readies LAST (every child already exists).

signal died

var is_dead: bool = false

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
	if swipe != null:
		swipe.tapped.connect(_on_tap)

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

func _process(_delta: float) -> void:
	# Restart (Enter) only acts on the death screen.
	if is_dead and Input.is_action_just_pressed("restart"):
		restart()

## Called by an obstacle when it touches the player.
func die() -> void:
	if is_dead:
		return
	is_dead = true
	died.emit()

func _on_tap() -> void:
	if is_dead:
		restart()

func restart() -> void:
	get_tree().reload_current_scene()
