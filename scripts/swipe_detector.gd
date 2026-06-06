extends Node
## Translates touch gestures into high-level signals.
## Records the touch start; on release, the dominant axis decides the gesture:
##   swipe up    -> swiped_up    (jump)
##   swipe down  -> swiped_down  (slide)
##   swipe left  -> swiped_left  (lane left)
##   swipe right -> swiped_right (lane right)
##   negligible  -> tapped       (restart)
## Keyboard remains the primary path for desktop testing.

signal swiped_up
signal swiped_down
signal swiped_left
signal swiped_right
signal tapped

@export var swipe_threshold: float = 60.0   # min travel (px) to count as a swipe

var _touch_start: Vector2 = Vector2.ZERO
var _touching: bool = false

func _ready() -> void:
	add_to_group("swipe_input")

## True while a finger is held on the screen (used for touch-hold glide).
func is_holding() -> bool:
	return _touching

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		if event.pressed:
			_touch_start = event.position
			_touching = true
		elif _touching:
			_touching = false
			_resolve(event.position - _touch_start)

func _resolve(delta: Vector2) -> void:
	if abs(delta.x) < swipe_threshold and abs(delta.y) < swipe_threshold:
		tapped.emit()
		return
	if abs(delta.x) > abs(delta.y):
		# Horizontal swipe.
		if delta.x > 0.0:
			swiped_right.emit()
		else:
			swiped_left.emit()
	else:
		# Vertical swipe.
		if delta.y < 0.0:
			swiped_up.emit()
		else:
			swiped_down.emit()
