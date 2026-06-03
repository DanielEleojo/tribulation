extends CanvasLayer
## HUD: shows the running distance, and a death screen on game over.

@onready var distance_label: Label = $Distance
@onready var death_label: Label = $DeathLabel
@onready var qi_bar: ProgressBar = $QiBar
@onready var flash_rect: ColorRect = $Flash
@onready var souls_label: Label = $SoulsLabel

var player
var _souls: int = 0

func _ready() -> void:
	add_to_group("hud")
	player = get_tree().get_first_node_in_group("player")
	death_label.visible = false
	flash_rect.color = Color(1, 1, 1, 0)

## Brief full-screen color flash (gate feedback).
func flash(c: Color) -> void:
	flash_rect.color = Color(c.r, c.g, c.b, 0.45)
	var tw := flash_rect.create_tween()
	tw.tween_property(flash_rect, "color:a", 0.0, 0.4)

## Called by the game coordinator whenever Qi changes (and once at start).
func on_qi_changed(qi: float, qi_max: float) -> void:
	qi_bar.max_value = qi_max
	qi_bar.value = qi

## Called by the game coordinator whenever Demon Souls change (and once at start).
func on_souls_changed(souls: int) -> void:
	_souls = souls
	souls_label.text = "Souls: %d" % souls

func _process(_delta: float) -> void:
	if player == null:
		return
	distance_label.text = "Distance: %d m" % player.get_distance()

## Called by the game coordinator when the player dies.
func on_death() -> void:
	var dist := 0
	if player != null:
		dist = player.get_distance()
	death_label.text = "YOU DIED\n\nDistance: %d m\nDemon Souls: %d\n\nEnter / tap to retry\n[ Watch ad to continue — coming soon ]" % [dist, _souls]
	death_label.visible = true
