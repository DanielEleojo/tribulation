extends CanvasLayer
## HUD: shows the running distance, and a death screen on game over.

@onready var distance_label: Label = $Distance
@onready var death_label: Label = $DeathLabel

var player

func _ready() -> void:
	add_to_group("hud")
	player = get_tree().get_first_node_in_group("player")
	death_label.visible = false

func _process(_delta: float) -> void:
	if player == null:
		return
	distance_label.text = "Distance: %d m" % player.get_distance()

## Called by the game coordinator when the player dies.
func on_death() -> void:
	var dist := 0
	if player != null:
		dist = player.get_distance()
	death_label.text = "YOU DIED\nDistance: %d m\nEnter / tap to retry" % dist
	death_label.visible = true
