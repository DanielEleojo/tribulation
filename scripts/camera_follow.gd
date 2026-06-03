extends Camera3D
## Chase camera: sits BEHIND and ABOVE the player, looking forward (-Z).
## Height and the look-target height are fixed, so the view does NOT bob when
## the player jumps. X follows the player partially (smoothed) for stability.

@export var back: float = 8.0          # distance behind the player (+Z)
@export var height: float = 5.0        # fixed camera height (locked — no jump bob)
@export var x_follow: float = 0.5      # how much of the player's X the camera tracks
@export var look_ahead: float = 12.0   # how far ahead the camera aims (-Z)
@export var follow_sharp: float = 8.0  # X smoothing rate
@export var shake_translate: float = 0.6   # max positional shake (units) at full trauma
@export var shake_roll: float = 0.09       # max roll shake (radians) at full trauma
@export var trauma_decay: float = 2.4      # how fast trauma fades (per sec)

var player: Node3D
var _trauma: float = 0.0

func _ready() -> void:
	make_current()
	add_to_group("camera")
	player = get_tree().get_first_node_in_group("player")

## Add screen-shake energy (0..1). Squared on apply for a punchy falloff.
func add_trauma(amount: float) -> void:
	_trauma = minf(1.0, _trauma + amount)

func _process(delta: float) -> void:
	if player == null:
		player = get_tree().get_first_node_in_group("player")
		if player == null:
			return
	var target_x: float = player.global_position.x * x_follow
	global_position.x = lerp(global_position.x, target_x, clampf(follow_sharp * delta, 0.0, 1.0))
	global_position.y = height
	global_position.z = player.global_position.z + back
	look_at(Vector3(player.global_position.x * 0.5, 1.0, player.global_position.z - look_ahead), Vector3.UP)

	# Trauma-based screen shake, applied after aiming so it jitters the framing.
	if _trauma > 0.0:
		_trauma = maxf(0.0, _trauma - trauma_decay * delta)
		var s: float = _trauma * _trauma
		global_position.x += randf_range(-1.0, 1.0) * s * shake_translate
		global_position.y += randf_range(-1.0, 1.0) * s * shake_translate
		rotate_object_local(Vector3(0.0, 0.0, 1.0), randf_range(-1.0, 1.0) * s * shake_roll)
