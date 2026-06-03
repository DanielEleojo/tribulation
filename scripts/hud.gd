extends CanvasLayer
## HUD: shows the running distance, read from the player each frame.

@onready var distance_label: Label = $Distance

var player

func _ready() -> void:
	player = get_tree().get_first_node_in_group("player")

func _process(_delta: float) -> void:
	if player == null:
		return
	distance_label.text = "Distance: %d m" % player.get_distance()
