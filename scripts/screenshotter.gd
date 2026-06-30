extends Node
## DEV-ONLY screenshot helper. Press F12 while the game runs to save a PNG at the
## exact App Store 6.5" iPhone size (1242x2688), regardless of the window being
## clamped by the display. Center-crops to the target aspect so nothing is stretched.
## No-op in exported release builds.

const W := 1242
const H := 2688
var _n := 0

func _input(event: InputEvent) -> void:
	if not OS.is_debug_build():
		return
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F12:
		_shot()

func _shot() -> void:
	var img := get_viewport().get_texture().get_image()
	if img == null:
		return
	var iw := img.get_width()
	var ih := img.get_height()
	if iw <= 0 or ih <= 0:
		return
	# scale to COVER the target, then center-crop -> exact size, no distortion
	var s: float = maxf(float(W) / float(iw), float(H) / float(ih))
	img.resize(int(round(iw * s)), int(round(ih * s)), Image.INTERPOLATE_LANCZOS)
	var x: int = int((img.get_width() - W) / 2.0)
	var y: int = int((img.get_height() - H) / 2.0)
	img = img.get_region(Rect2i(x, y, W, H))
	DirAccess.make_dir_recursive_absolute("user://screenshots")
	_n += 1
	var path := "user://screenshots/shot_%03d.png" % _n
	img.save_png(path)
	print("[shot] saved ", ProjectSettings.globalize_path(path), "  (%dx%d)" % [W, H])
