extends CanvasLayer
## The Heavenly Net: four dark edges that close inward as the net tightens.
## net = 0 -> edges invisible (fully open); net = 1 -> edges meet (closed = death).
## Driven by the game coordinator via on_net_changed().

const HALF_W: float = 640.0     # design-space half width (1280 base, canvas_items stretch)
const HALF_H: float = 360.0     # design-space half height (720 base)
const NET_COLOR := Color(0.55, 0.03, 0.06, 0.82)

var _top: ColorRect
var _bottom: ColorRect
var _left: ColorRect
var _right: ColorRect

func _ready() -> void:
	add_to_group("net_overlay")
	layer = 0   # above the 3D world, below the HUD text (HUD is layer 1)
	_top = _make_edge()
	_bottom = _make_edge()
	_left = _make_edge()
	_right = _make_edge()
	_set_anchors(_top, 0.0, 0.0, 1.0, 0.0)      # full width, pinned to top
	_set_anchors(_bottom, 0.0, 1.0, 1.0, 1.0)   # full width, pinned to bottom
	_set_anchors(_left, 0.0, 0.0, 0.0, 1.0)     # full height, pinned to left
	_set_anchors(_right, 1.0, 0.0, 1.0, 1.0)    # full height, pinned to right
	on_net_changed(0.0)

func _make_edge() -> ColorRect:
	var r := ColorRect.new()
	r.color = NET_COLOR
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(r)
	return r

func _set_anchors(r: ColorRect, l: float, t: float, rr: float, b: float) -> void:
	r.anchor_left = l
	r.anchor_top = t
	r.anchor_right = rr
	r.anchor_bottom = b
	r.offset_left = 0.0
	r.offset_top = 0.0
	r.offset_right = 0.0
	r.offset_bottom = 0.0

## Resize the edges to reflect how closed the net is (0..1).
func on_net_changed(net: float) -> void:
	var h: float = net * HALF_H
	var w: float = net * HALF_W
	_top.offset_bottom = h
	_bottom.offset_top = -h
	_left.offset_right = w
	_right.offset_left = -w
