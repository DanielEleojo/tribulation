extends Node
## Translates touch gestures into high-level signals the game can react to.
## Records the touch start position; on release decides:
##   swipe up   (moved up past threshold)   -> swiped_up   (jump)
##   swipe down (moved down past threshold) -> swiped_down (slide)
##   negligible movement                    -> tapped      (restart)
## Keyboard remains the primary path for desktop testing; this is for touch.

signal swiped_up
signal swiped_down
signal tapped

@export var swipe_threshold: float = 80.0   # min vertical travel (px) to count as a swipe

var _touch_start: Vector2 = Vector2.ZERO
var _touching: bool = false

func _ready() -> void:
	add_to_group("swipe_input")

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		if event.pressed:
			_touch_start = event.position
			_touching = true
		elif _touching:
			_touching = false
			var dy: float = event.position.y - _touch_start.y
			if dy < -swipe_threshold:
				swiped_up.emit()
			elif dy > swipe_threshold:
				swiped_down.emit()
			else:
				tapped.emit()
