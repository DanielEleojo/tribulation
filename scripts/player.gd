extends CharacterBody2D
## Player: auto-runs to the RIGHT at a constant speed, with gravity.
## Placeholder visual is a ColorRect (see game.tscn). Jump/slide come in later steps.

@export var run_speed: float = 350.0   # constant horizontal speed (px/sec)
@export var gravity: float = 1400.0    # downward acceleration (px/sec^2)

var start_x: float = 0.0

func _ready() -> void:
	add_to_group("player")
	start_x = global_position.x

func _physics_process(delta: float) -> void:
	# Always run right at a constant speed.
	velocity.x = run_speed
	# Apply gravity every frame; landing on the floor cancels downward velocity.
	velocity.y += gravity * delta
	move_and_slide()

## Distance readout: world X since spawn, mapped to an integer "meters" value.
func get_distance() -> int:
	return int(max(0.0, (global_position.x - start_x) / 10.0))
