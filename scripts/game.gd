extends Node2D
## Root coordinator: owns the dead/alive state and wires up death + restart.
## Wiring is done here (the root readies LAST, so every child already exists),
## which avoids _ready ordering pitfalls.

signal died

var is_dead: bool = false

func _ready() -> void:
	add_to_group("game")
	var player := get_tree().get_first_node_in_group("player")
	var hud := get_tree().get_first_node_in_group("hud")
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if player != null:
		died.connect(player.on_death)
	if hud != null:
		died.connect(hud.on_death)
	if swipe != null:
		swipe.tapped.connect(_on_tap)

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
