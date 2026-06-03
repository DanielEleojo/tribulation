extends CharacterBody2D
## Player: auto-runs to the RIGHT at a constant speed, with gravity.
## Jump:  "jump" action (Space) or swipe-up; only while on the floor. Cancels a slide.
## Slide: "slide" action (Down / S) or swipe-down.
##        - On the ground: briefly lowers the body for ~0.5s, then restores.
##        - In the air: fast-fall (dive) straight down, then slides on landing.
## Placeholder visual is a ColorRect (see game.tscn).

@export var run_speed: float = 350.0       # constant horizontal speed (px/sec)
@export var gravity: float = 2000.0        # downward acceleration (px/sec^2) — higher = snappier jump
@export var jump_velocity: float = -760.0  # upward velocity applied on jump (px/sec)
@export var fast_fall_speed: float = 1800.0  # downward velocity when sliding mid-air (px/sec)

const SLIDE_DURATION: float = 0.5          # seconds the slide lasts
const STAND_HEIGHT: float = 60.0           # normal body height
const SLIDE_HEIGHT: float = 30.0           # crouched body height
const BODY_WIDTH: float = 40.0
const STAND_COLOR := Color(0.95, 0.82, 0.2, 1.0)   # gold when standing/running
const SLIDE_COLOR := Color(0.3, 0.8, 0.9, 1.0)     # cyan while sliding

@onready var visual: ColorRect = $Visual
@onready var collision: CollisionShape2D = $Collision

var start_x: float = 0.0
var is_sliding: bool = false
var slide_time_left: float = 0.0
var _pending_slide: bool = false           # queued slide for when a fast-fall lands
var _was_on_floor: bool = false

func _ready() -> void:
	add_to_group("player")
	start_x = global_position.x
	_set_height(STAND_HEIGHT, STAND_COLOR)
	# Wire touch swipes to the same actions.
	var swipe := get_tree().get_first_node_in_group("swipe_input")
	if swipe != null:
		swipe.swiped_up.connect(try_jump)
		swipe.swiped_down.connect(start_slide)

func _physics_process(delta: float) -> void:
	# Always run right at a constant speed.
	velocity.x = run_speed
	# Apply gravity every frame; landing on the floor cancels downward velocity.
	velocity.y += gravity * delta

	var grounded := is_on_floor()

	# A queued fast-fall slide fires the moment we touch down.
	if grounded and not _was_on_floor and _pending_slide:
		_pending_slide = false
		start_slide()

	# Slide countdown.
	if is_sliding:
		slide_time_left -= delta
		if slide_time_left <= 0.0:
			_end_slide()

	# Keyboard input.
	if Input.is_action_just_pressed("jump"):
		try_jump()
	if Input.is_action_just_pressed("slide"):
		start_slide()

	_was_on_floor = grounded
	move_and_slide()

## Apply an upward impulse when grounded. Jumping mid-slide cancels the slide.
func try_jump() -> void:
	if not is_on_floor():
		return
	if is_sliding:
		_end_slide()
	_pending_slide = false
	velocity.y = jump_velocity

## Slide. On the ground: crouch. In the air: fast-fall and queue a slide on landing.
func start_slide() -> void:
	if is_on_floor():
		if is_sliding:
			return
		is_sliding = true
		slide_time_left = SLIDE_DURATION
		_set_height(SLIDE_HEIGHT, SLIDE_COLOR)
	else:
		# Dive straight down, then slide once we land.
		velocity.y = max(velocity.y, fast_fall_speed)
		_pending_slide = true

func _end_slide() -> void:
	is_sliding = false
	_set_height(STAND_HEIGHT, STAND_COLOR)

## Resize collision + visual, keeping the player's feet at the body origin (y=0).
func _set_height(h: float, col: Color) -> void:
	var shape := collision.shape as RectangleShape2D
	shape.size = Vector2(BODY_WIDTH, h)
	collision.position = Vector2(0.0, -h * 0.5)
	visual.offset_left = -BODY_WIDTH * 0.5
	visual.offset_right = BODY_WIDTH * 0.5
	visual.offset_top = -h
	visual.offset_bottom = 0.0
	visual.color = col

## Distance readout: world X since spawn, mapped to an integer "meters" value.
func get_distance() -> int:
	return int(max(0.0, (global_position.x - start_x) / 10.0))
