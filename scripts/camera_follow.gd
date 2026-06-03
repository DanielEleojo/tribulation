extends Camera2D
## Camera follows the player on X only. Y is LOCKED to a fixed height so the
## view does not bob when the player jumps.

@export var x_offset: float = 350.0     # shifts the view ahead; keeps player toward the left
@export var locked_y: float = 360.0     # fixed world Y for the camera center

var player: Node2D

func _ready() -> void:
	make_current()
	player = get_tree().get_first_node_in_group("player")

func _process(_delta: float) -> void:
	if player == null:
		return
	global_position.x = player.global_position.x + x_offset
	global_position.y = locked_y
