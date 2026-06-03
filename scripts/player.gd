extends CharacterBody2D
## Player: auto-runs to the RIGHT at a constant speed, with gravity.
## Jump: "jump" action (Space) or swipe-up; only fires while on the floor.
## Placeholder visual is a ColorRect (see game.tscn).

@export var run_speed: float = 350.0     # constant horizontal speed (px/sec)
@export var gravity: float = 1400.0      # downward acceleration (px/sec^2)
@export var jump_velocity: float = -700.0  # upward velocity applied on jump (px/sec)

var start_x: float = 0.0

func _ready() -> void:
	add_to_group("player")
	start_x = global_position.x
	# Wire touch swipe-up to the same jump.
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if swipe != null:
		swipe.swiped_up.connect(try_jump)

func _physics_process(delta: float) -> void:
	# Always run right at a constant speed.
	velocity.x = run_speed
	# Apply gravity every frame; landing on the floor cancels downward velocity.
	velocity.y += gravity * delta
	# Keyboard jump.
	if Input.is_action_just_pressed("jump"):
		try_jump()
	move_and_slide()

## Apply an upward impulse, but only when grounded (no mid-air jumps).
func try_jump() -> void:
	if is_on_floor():
		velocity.y = jump_velocity

## Distance readout: world X since spawn, mapped to an integer "meters" value.
func get_distance() -> int:
	return int(max(0.0, (global_position.x - start_x) / 10.0))
