extends Node2D
## Obstacle spawner. Places obstacles AHEAD of the player at world positions,
## alternating two placeholder types:
##   - ground block (red)   -> jump over
##   - overhead bar (purple) -> slide under
## Spawn frequency increases over time (interval shrinks to a floor).
## Obstacles queue_free() once well behind the player. Obstacles are Area2D
## triggers (no physics push); contact with the player causes death.

const GROUND_TOP_Y: float = 560.0
const SPAWN_AHEAD: float = 1050.0       # px ahead of player (just off the right edge)
const DESPAWN_BEHIND: float = 700.0     # px behind player before removal

# Block (jump over): rests on the ground.
const BLOCK_WIDTH: float = 50.0
const BLOCK_HEIGHT: float = 60.0
const BLOCK_COLOR := Color(0.85, 0.22, 0.22, 1.0)

# Bar (slide under): floats at head height, leaving a gap to slide through.
const BAR_WIDTH: float = 80.0
const BAR_HEIGHT: float = 40.0
const BAR_BOTTOM_Y: float = 515.0       # bottom edge; standing player (top 500) hits it, slider (top 530) clears
const BAR_COLOR := Color(0.45, 0.35, 0.9, 1.0)

# Frequency ramp.
@export var start_interval: float = 1.6   # seconds between spawns at the start
@export var min_interval: float = 0.8     # fastest spawn rate
@export var ramp_time: float = 60.0       # seconds to reach min_interval

var player: Node2D
var game
var _elapsed: float = 0.0
var _timer: float = 0.0
var _spawn_index: int = 0

func _ready() -> void:
	game = get_tree().get_first_node_in_group("game")
	_timer = start_interval

func _process(delta: float) -> void:
	# Player joins its group after this node readies, so look it up lazily.
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	if game == null:
		game = get_tree().get_first_node_in_group("game")
	if game != null and game.is_dead:
		return
	_elapsed += delta
	_timer -= delta
	if _timer <= 0.0:
		_spawn()
		_timer = _current_interval()
	_cleanup()

func _current_interval() -> float:
	var t: float = clampf(_elapsed / ramp_time, 0.0, 1.0)
	return lerpf(start_interval, min_interval, t)

func _spawn() -> void:
	# Alternate: even -> block (jump), odd -> bar (slide).
	var is_block: bool = (_spawn_index % 2 == 0)
	_spawn_index += 1
	var obs := _make_obstacle(is_block)
	obs.position = Vector2(player.global_position.x + SPAWN_AHEAD, 0.0)
	add_child(obs)

func _make_obstacle(is_block: bool) -> Area2D:
	var area := Area2D.new()

	var rect := ColorRect.new()
	var shape := CollisionShape2D.new()
	var rect_shape := RectangleShape2D.new()

	if is_block:
		var top_y := GROUND_TOP_Y - BLOCK_HEIGHT
		rect.size = Vector2(BLOCK_WIDTH, BLOCK_HEIGHT)
		rect.position = Vector2(-BLOCK_WIDTH * 0.5, top_y)
		rect.color = BLOCK_COLOR
		rect_shape.size = Vector2(BLOCK_WIDTH, BLOCK_HEIGHT)
		shape.position = Vector2(0.0, top_y + BLOCK_HEIGHT * 0.5)
	else:
		var top_y := BAR_BOTTOM_Y - BAR_HEIGHT
		rect.size = Vector2(BAR_WIDTH, BAR_HEIGHT)
		rect.position = Vector2(-BAR_WIDTH * 0.5, top_y)
		rect.color = BAR_COLOR
		rect_shape.size = Vector2(BAR_WIDTH, BAR_HEIGHT)
		shape.position = Vector2(0.0, top_y + BAR_HEIGHT * 0.5)

	shape.shape = rect_shape
	area.add_child(rect)
	area.add_child(shape)
	area.body_entered.connect(_on_obstacle_body_entered)
	return area

func _on_obstacle_body_entered(body: Node) -> void:
	if body.is_in_group("player") and game != null:
		game.die()

func _cleanup() -> void:
	var kill_x: float = player.global_position.x - DESPAWN_BEHIND
	for child in get_children():
		if child.position.x < kill_x:
			child.queue_free()
