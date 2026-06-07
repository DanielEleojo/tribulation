extends Node
## Background score: loops assets/music/<theme> if present, and shapes it live —
## pitch rises with run speed, swells during a Tribulation, ducks on death.
## No-op if no track exists (drop a real erhu/guqin loop in assets/music/ to replace).

const MUSIC_DIR := "res://assets/music/"
const EXTS := [".ogg", ".wav", ".mp3"]
const NAMES := ["theme", "music", "bgm"]

var _player: AudioStreamPlayer
var _game
var _pl

func _ready() -> void:
	add_to_group("music")
	var stream := _load()
	if stream == null:
		return
	if stream is AudioStreamWAV:
		stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
		stream.loop_begin = 0
		var bps := 2 if stream.format == AudioStreamWAV.FORMAT_16_BITS else 1
		var ch := 2 if stream.stereo else 1
		stream.loop_end = int(stream.data.size() / (bps * ch))   # bytes -> frames
	_player = AudioStreamPlayer.new()
	_player.stream = stream
	_player.volume_db = -9.0
	add_child(_player)
	_player.play()

func _load() -> AudioStream:
	for nm in NAMES:
		for e in EXTS:
			var p: String = MUSIC_DIR + str(nm) + str(e)
			if ResourceLoader.exists(p):
				return load(p)
	return null

func _process(delta: float) -> void:
	if _player == null:
		return
	if _game == null:
		_game = get_tree().get_first_node_in_group("game")
	if _pl == null:
		_pl = get_tree().get_first_node_in_group("player")
	var pitch := 1.0
	var vol := -9.0
	if _pl != null and _pl.has_method("get_speed_fraction"):
		pitch = lerpf(0.95, 1.12, _pl.get_speed_fraction())
	if _game != null:
		if "is_dead" in _game and _game.is_dead:
			vol = -24.0
		elif _game.has_method("in_tribulation") and _game.in_tribulation():
			pitch += 0.08
			vol = -3.0
	var k := clampf(2.0 * delta, 0.0, 1.0)
	_player.pitch_scale = lerpf(_player.pitch_scale, pitch, k)
	_player.volume_db = lerpf(_player.volume_db, vol, k)
