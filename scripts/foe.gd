extends Node3D
## Minimal "alive" motion for a placeholder disciple: a running bounce + slight
## sway, so foes read as moving martial artists rather than static capsules.

var _t: float = 0.0
var _phase: float = 0.0

func _ready() -> void:
	_phase = randf() * TAU   # desync so a row of foes don't bob in lockstep

func _process(delta: float) -> void:
	_t += delta * 8.0
	position.y = absf(sin(_t + _phase)) * 0.14
	rotation.y = sin((_t + _phase) * 0.5) * 0.18
