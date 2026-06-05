extends Control
## Studio splash: hold the Vellicade Games logo for a fixed time, fade, then
## continue to the game (title screen). Set as the project's main scene.

@export var duration: float = 2.0     # seconds to hold the splash
@export var fade_time: float = 0.4

func _ready() -> void:
	await get_tree().create_timer(duration).timeout
	var tw := create_tween()
	tw.tween_property(self, "modulate:a", 0.0, fade_time)
	await tw.finished
	get_tree().change_scene_to_file("res://scenes/game.tscn")
