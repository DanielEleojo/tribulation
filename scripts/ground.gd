extends Node2D
## Infinite ground via RECYCLING segments.
## A handful of wide ground segments are laid end-to-end. When a segment passes
## behind the camera's left edge, it is repositioned to the right of the
## rightmost segment so the ground never shows a gap.

const SEGMENT_WIDTH: float = 1280.0
const SEGMENT_HEIGHT: float = 200.0
const GROUND_TOP_Y: float = 560.0          # world Y of the ground's top surface
const SEGMENT_COUNT: int = 4
const RECYCLE_MARGIN: float = 100.0        # extra slack before recycling, avoids popping
const GROUND_COLOR := Color(0.20, 0.14, 0.11, 1.0)
const STRIPE_COLOR := Color(0.30, 0.22, 0.17, 1.0)  # lighter marks so motion is visible
const STRIPE_SPACING: float = 128.0
const STRIPE_WIDTH: float = 14.0
const SURFACE_COLOR := Color(0.42, 0.55, 0.30, 1.0)  # green-ish top edge (the "grass" line)
const SURFACE_HEIGHT: float = 10.0

var segments: Array[StaticBody2D] = []

func _ready() -> void:
	for i in range(SEGMENT_COUNT):
		var seg := _make_segment()
		seg.position.x = float(i) * SEGMENT_WIDTH
		add_child(seg)
		segments.append(seg)

func _make_segment() -> StaticBody2D:
	var body := StaticBody2D.new()

	# Placeholder visual: a wide ColorRect sitting at the ground's top surface.
	var rect := ColorRect.new()
	rect.size = Vector2(SEGMENT_WIDTH, SEGMENT_HEIGHT)
	rect.position = Vector2(0.0, GROUND_TOP_Y)
	rect.color = GROUND_COLOR
	body.add_child(rect)

	# Vertical stripe markers so the ground's leftward scroll is clearly visible.
	var x := 0.0
	while x < SEGMENT_WIDTH:
		var stripe := ColorRect.new()
		stripe.size = Vector2(STRIPE_WIDTH, SEGMENT_HEIGHT)
		stripe.position = Vector2(x, GROUND_TOP_Y)
		stripe.color = STRIPE_COLOR
		body.add_child(stripe)
		x += STRIPE_SPACING

	# A bright top "surface" line along the walkable edge.
	var surface := ColorRect.new()
	surface.size = Vector2(SEGMENT_WIDTH, SURFACE_HEIGHT)
	surface.position = Vector2(0.0, GROUND_TOP_Y)
	surface.color = SURFACE_COLOR
	body.add_child(surface)

	# Matching collision rectangle (centered on the visual).
	var shape := CollisionShape2D.new()
	var rect_shape := RectangleShape2D.new()
	rect_shape.size = Vector2(SEGMENT_WIDTH, SEGMENT_HEIGHT)
	shape.shape = rect_shape
	shape.position = Vector2(SEGMENT_WIDTH * 0.5, GROUND_TOP_Y + SEGMENT_HEIGHT * 0.5)
	body.add_child(shape)

	return body

func _physics_process(_delta: float) -> void:
	var cam := get_viewport().get_camera_2d()
	if cam == null:
		return
	var half_view := get_viewport_rect().size.x * 0.5
	var visible_left := cam.global_position.x - half_view - RECYCLE_MARGIN

	for seg in segments:
		# If a segment's right edge has scrolled past the left of the view, recycle it.
		if seg.position.x + SEGMENT_WIDTH < visible_left:
			seg.position.x = _rightmost_x() + SEGMENT_WIDTH

func _rightmost_x() -> float:
	var m := -INF
	for seg in segments:
		m = max(m, seg.position.x)
	return m
