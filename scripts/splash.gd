extends Control
## Studio splash: hold the Vallicade Games logo for a beat, fade, then enter the
## game. It mirrors the iOS launch image (same art, same cover-fill), so the OS
## launch screen flows seamlessly into this held splash before the title appears.
## Tap / any key skips it.

@export var duration: float = 2.0     # seconds to hold the logo
@export var fade_time: float = 0.4

var _done: bool = false

func _ready() -> void:
	await get_tree().create_timer(duration).timeout
	_go()

func _go() -> void:
	if _done:
		return
	_done = true
	var tw := create_tween()
	tw.tween_property(self, "modulate:a", 0.0, fade_time)
	await tw.finished
	get_tree().change_scene_to_file("res://scenes/game.tscn")

func _input(event: InputEvent) -> void:
	if (event is InputEventScreenTouch and event.pressed) or (event is InputEventKey and event.pressed):
		_go()
